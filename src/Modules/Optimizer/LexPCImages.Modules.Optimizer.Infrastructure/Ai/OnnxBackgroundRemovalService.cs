using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Ai;

/// <summary>
/// Segmentación con el modelo RMBG-1.4 sobre ONNX Runtime. La sesión es cara de crear, así que
/// el servicio se registra como singleton y serializa las inferencias con un cerrojo.
/// </summary>
public sealed class OnnxBackgroundRemovalService : IBackgroundRemovalService, IDisposable
{
    private const int ModelInputSize = 1024;
    private const float NormalizationMean = 0.5f;
    private const float NormalizationStd = 1.0f;
    private const string InputName = "input";
    private const string OutputName = "output";

    private readonly InferenceSession _session;
    private readonly Lock _sessionLock = new();

    public OnnxBackgroundRemovalService(string modelPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelPath);
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"ONNX model not found at '{modelPath}'. Download a model (e.g. briaai/RMBG-1.4 FP16) and place it at that path.",
                modelPath);
        }

        var sessionOptions = new SessionOptions
        {
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };

        _session = new InferenceSession(modelPath, sessionOptions);
    }

    public Task<MaskResult> RemoveBackgroundAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        var inputTensor = Preprocess(image);
        cancellationToken.ThrowIfCancellationRequested();

        MaskResult mask;
        lock (_sessionLock)
        {
            using var results = _session.Run([NamedOnnxValue.CreateFromTensor(InputName, inputTensor)]);
            var output = results.First(result => result.Name == OutputName);
            mask = Postprocess(image, output.AsTensor<float>());
        }
        return Task.FromResult(mask);
    }

    /// <summary>Reescala a la entrada del modelo y normaliza a un tensor NCHW.</summary>
    private static DenseTensor<float> Preprocess(DecodedImage image)
    {
        using var source = RgbaImageInterop.ToImage(image);
        source.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(ModelInputSize, ModelInputSize),
            Mode = ImgResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var tensor = new DenseTensor<float>([1, 3, ModelInputSize, ModelInputSize]);
        source.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < ModelInputSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < ModelInputSize; x++)
                {
                    var pixel = row[x];
                    tensor[0, 0, y, x] = Normalize(pixel.R);
                    tensor[0, 1, y, x] = Normalize(pixel.G);
                    tensor[0, 2, y, x] = Normalize(pixel.B);
                }
            }
        });
        return tensor;
    }

    private static float Normalize(byte channel) => ((channel / 255f) - NormalizationMean) / NormalizationStd;

    /// <summary>Reescala la máscara del tamaño del modelo al de la imagen original.</summary>
    private static MaskResult Postprocess(DecodedImage original, Tensor<float> output)
    {
        using var maskImage = new Image<L8>(ModelInputSize, ModelInputSize);
        maskImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    row[x] = new L8((byte)(Math.Clamp(output[0, 0, y, x], 0f, 1f) * 255f));
                }
            }
        });

        maskImage.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(original.Width, original.Height),
            Mode = ImgResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var maskValues = new float[original.Width * original.Height];
        var index = 0;
        maskImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    maskValues[index++] = row[x].PackedValue / 255f;
                }
            }
        });
        return new MaskResult(original.Width, original.Height, maskValues);
    }

    public void Dispose() => _session.Dispose();
}
