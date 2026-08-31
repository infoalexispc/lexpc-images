using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

/// <summary>
/// Escala la imagen manteniendo la proporción y rellena hasta el tamaño del slot con el color
/// dominante del borde, de modo que el relleno pasa desapercibido sobre fondos lisos.
/// </summary>
public sealed class ImageSharpPadder : IImagePadder
{
    private const int BorderSampleCount = 16;

    private readonly DownscaleFilter _filter;

    public ImageSharpPadder(IOptions<OptimizerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _filter = options.Value.DownscaleFilter;
    }

    public PaddedImage Pad(DecodedImage image, int targetWidth, int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);

        var background = DetectBackgroundColor(image);
        var (scaledWidth, scaledHeight) = ScaleToFit(image.Width, image.Height, targetWidth, targetHeight);
        var offsetX = (targetWidth - scaledWidth) / 2;
        var offsetY = (targetHeight - scaledHeight) / 2;

        var sampler = ResamplerSelector.For(
            _filter, image.Width, image.Height, scaledWidth, scaledHeight);

        using var sourceImage = RgbaImageInterop.ToImage(image);
        sourceImage.Mutate(context => context.Resize(new ResizeOptions
        {
            Size = new Size(scaledWidth, scaledHeight),
            Mode = ImgResizeMode.Stretch,
            Sampler = sampler,
        }));
        var scaled = RgbaImageInterop.ToDecodedImage(sourceImage);

        var output = new byte[targetWidth * targetHeight * RgbaImageInterop.BytesPerPixel];
        FillBackground(output, background);
        CopyInto(output, targetWidth, scaled, offsetX, offsetY);

        return new PaddedImage(new DecodedImage(targetWidth, targetHeight, output), offsetX, offsetY);
    }

    private static (int Width, int Height) ScaleToFit(
        int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        var sourceAspect = (double)sourceWidth / sourceHeight;
        var targetAspect = (double)targetWidth / targetHeight;

        return sourceAspect > targetAspect
            ? (targetWidth, Math.Max(1, (int)Math.Round(targetWidth / sourceAspect)))
            : (Math.Max(1, (int)Math.Round(targetHeight * sourceAspect)), targetHeight);
    }

    /// <summary>Color más repetido a lo largo de los cuatro bordes de la imagen.</summary>
    private static Rgba32 DetectBackgroundColor(DecodedImage image)
    {
        var samples = new Dictionary<int, int>();
        for (var i = 0; i < BorderSampleCount; i++)
        {
            var t = (double)i / Math.Max(1, BorderSampleCount - 1);
            var x = (int)Math.Round(t * (image.Width - 1));
            var y = (int)Math.Round(t * (image.Height - 1));
            Sample(image, x, 0, samples);
            Sample(image, x, image.Height - 1, samples);
            Sample(image, 0, y, samples);
            Sample(image, image.Width - 1, y, samples);
        }

        if (samples.Count == 0)
        {
            return new Rgba32(255, 255, 255, 255);
        }

        var packed = samples.MaxBy(entry => entry.Value).Key;
        return new Rgba32(
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF),
            byte.MaxValue);
    }

    private static void Sample(DecodedImage image, int x, int y, Dictionary<int, int> samples)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height)
        {
            return;
        }

        var offset = ((y * image.Width) + x) * RgbaImageInterop.BytesPerPixel;
        var packed = (image.Rgba[offset] << 16) | (image.Rgba[offset + 1] << 8) | image.Rgba[offset + 2];
        samples[packed] = samples.GetValueOrDefault(packed) + 1;
    }

    private static void FillBackground(byte[] output, Rgba32 background)
    {
        for (var i = 0; i < output.Length; i += RgbaImageInterop.BytesPerPixel)
        {
            output[i] = background.R;
            output[i + 1] = background.G;
            output[i + 2] = background.B;
            output[i + 3] = background.A;
        }
    }

    private static void CopyInto(byte[] output, int targetWidth, DecodedImage source, int offsetX, int offsetY)
    {
        var rowBytes = source.Width * RgbaImageInterop.BytesPerPixel;
        for (var y = 0; y < source.Height; y++)
        {
            var sourceOffset = y * rowBytes;
            var destinationOffset = (((y + offsetY) * targetWidth) + offsetX) * RgbaImageInterop.BytesPerPixel;
            Array.Copy(source.Rgba, sourceOffset, output, destinationOffset, rowBytes);
        }
    }
}
