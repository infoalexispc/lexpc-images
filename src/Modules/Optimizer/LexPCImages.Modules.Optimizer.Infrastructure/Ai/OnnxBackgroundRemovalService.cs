using LexPCImages.Modules.Optimizer.Application.Abstractions;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Ai;

public sealed class OnnxBackgroundRemovalService : IBackgroundRemovalService, IDisposable
{
    private const int ModelInputSize = 1024;
    private const float NormalizationMean = 0.5f;
    private const float NormalizationStd = 1.0f;
    private const string InputName = "input";
    private const string OutputName = "output";

    private readonly InferenceSession _session;
    private readonly object _sessionLock = new();

    public OnnxBackgroundRemovalService(string modelPath)
    {
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
        cancellationToken.ThrowIfCancellationRequested();

        var inputTensor = Preprocess(image);
        cancellationToken.ThrowIfCancellationRequested();

        MaskResult mask;
        lock (_sessionLock)
        {
            using var results = _session.Run(new[] { NamedOnnxValue.CreateFromTensor(InputName, inputTensor) });
            using var outputValue = results.First(r => r.Name == OutputName);
            mask = Postprocess(image, outputValue.AsTensor<float>());
        }
        return Task.FromResult(mask);
    }

    private static DenseTensor<float> Preprocess(DecodedImage image)
    {
        using var source = WrapRgba(image);
        source.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(ModelInputSize, ModelInputSize),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var tensor = new DenseTensor<float>(new Memory<float>(new float[1 * 3 * ModelInputSize * ModelInputSize]), new[] { 1, 3, ModelInputSize, ModelInputSize });
        source.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < ModelInputSize; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < ModelInputSize; x++)
                {
                    var pixel = row[x];
                    var r = (pixel.R / 255f - NormalizationMean) / NormalizationStd;
                    var g = (pixel.G / 255f - NormalizationMean) / NormalizationStd;
                    var b = (pixel.B / 255f - NormalizationMean) / NormalizationStd;
                    tensor[0, 0, y, x] = r;
                    tensor[0, 1, y, x] = g;
                    tensor[0, 2, y, x] = b;
                }
            }
        });
        return tensor;
    }

    private static MaskResult Postprocess(DecodedImage original, Tensor<float> output)
    {
        var modelSize = ModelInputSize;
        var maskValues = new float[original.Width * original.Height];

        using var maskImage = new Image<L8>(modelSize, modelSize);
        maskImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    var value = output[0, 0, y, x];
                    var clamped = Math.Clamp(value, 0f, 1f);
                    row[x] = new L8((byte)(clamped * 255f));
                }
            }
        });
        maskImage.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(original.Width, original.Height),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var outIdx = 0;
        maskImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    maskValues[outIdx++] = row[x].PackedValue / 255f;
                }
            }
        });
        return new MaskResult(original.Width, original.Height, maskValues);
    }

    private static Image<Rgba32> WrapRgba(DecodedImage image)
    {
        var img = new Image<Rgba32>(image.Width, image.Height);
        img.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var sourceRowOffset = y * image.Width * 4;
                for (var x = 0; x < image.Width; x++)
                {
                    var i = sourceRowOffset + x * 4;
                    row[x] = new Rgba32(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
                }
            }
        });
        return img;
    }

    public void Dispose() => _session.Dispose();
}
