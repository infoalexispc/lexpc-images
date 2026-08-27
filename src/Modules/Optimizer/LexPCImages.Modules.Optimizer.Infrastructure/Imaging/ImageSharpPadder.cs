using LexPCImages.Modules.Optimizer.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ImgResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpPadder : IImagePadder
{
    private const int BorderSampleCount = 16;

    public PaddedImage Pad(DecodedImage image, int targetWidth, int targetHeight)
    {
        if (targetWidth <= 0 || targetHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetWidth),
                $"Target dimensions must be positive ({targetWidth}x{targetHeight}).");
        }

        var background = DetectBackgroundColor(image);
        var (resized, offsetX, offsetY) = ResizeKeepingAspect(
            image, targetWidth, targetHeight, background);

        return new PaddedImage(resized, offsetX, offsetY);
    }

    private static Rgba32 DetectBackgroundColor(DecodedImage image)
    {
        var samples = new Dictionary<long, int>();
        var width = image.Width;
        var height = image.Height;

        for (var i = 0; i < BorderSampleCount; i++)
        {
            var t = (double)i / Math.Max(1, BorderSampleCount - 1);
            var x = (int)Math.Round(t * (width - 1));
            var y = (int)Math.Round(t * (height - 1));
            Sample(image, 0, x, samples);
            Sample(image, height - 1, x, samples);
            Sample(image, y, 0, samples);
            Sample(image, y, width - 1, samples);
        }

        var dominant = samples.OrderByDescending(kv => kv.Value).First();
        var packed = dominant.Key;
        return new Rgba32(
            (byte)((packed >> 16) & 0xFF),
            (byte)((packed >> 8) & 0xFF),
            (byte)(packed & 0xFF),
            255);
    }

    private static void Sample(DecodedImage image, int y, int x, Dictionary<long, int> samples)
    {
        if ((uint)x >= (uint)image.Width || (uint)y >= (uint)image.Height)
        {
            return;
        }
        var offset = (y * image.Width + x) * 4;
        var r = image.Rgba[offset];
        var g = image.Rgba[offset + 1];
        var b = image.Rgba[offset + 2];
        var packed = ((long)r << 16) | ((long)g << 8) | b;
        samples.TryGetValue(packed, out var count);
        samples[packed] = count + 1;
    }

    private static (DecodedImage Resized, int OffsetX, int OffsetY) ResizeKeepingAspect(
        DecodedImage source,
        int targetWidth,
        int targetHeight,
        Rgba32 background)
    {
        var sourceWidth = source.Width;
        var sourceHeight = source.Height;
        var sourceAspect = (double)sourceWidth / sourceHeight;
        var targetAspect = (double)targetWidth / targetHeight;

        int scaledWidth;
        int scaledHeight;
        if (sourceAspect > targetAspect)
        {
            scaledWidth = targetWidth;
            scaledHeight = Math.Max(1, (int)Math.Round(targetWidth / sourceAspect));
        }
        else
        {
            scaledHeight = targetHeight;
            scaledWidth = Math.Max(1, (int)Math.Round(targetHeight * sourceAspect));
        }

        var offsetX = (targetWidth - scaledWidth) / 2;
        var offsetY = (targetHeight - scaledHeight) / 2;

        var output = new byte[targetWidth * targetHeight * 4];
        FillBackground(output, targetWidth, targetHeight, background);

        using var sourceImage = WrapRgba(source);
        sourceImage.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(scaledWidth, scaledHeight),
            Mode = ImgResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        }));

        var rgba = new byte[scaledWidth * scaledHeight * 4];
        sourceImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var dest = y * scaledWidth * 4;
                for (var x = 0; x < accessor.Width; x++)
                {
                    rgba[dest + x * 4] = row[x].R;
                    rgba[dest + x * 4 + 1] = row[x].G;
                    rgba[dest + x * 4 + 2] = row[x].B;
                    rgba[dest + x * 4 + 3] = row[x].A;
                }
            }
        });
        CopyIntoOutput(output, rgba, scaledWidth, scaledHeight, targetWidth, offsetX, offsetY);

        return (new DecodedImage(targetWidth, targetHeight, output), offsetX, offsetY);
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
                for (var x = 0; x < accessor.Width; x++)
                {
                    var i = sourceRowOffset + x * 4;
                    row[x] = new Rgba32(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
                }
            }
        });
        return img;
    }

    private static void FillBackground(byte[] output, int width, int height, Rgba32 background)
    {
        for (var i = 0; i < output.Length; i += 4)
        {
            output[i] = background.R;
            output[i + 1] = background.G;
            output[i + 2] = background.B;
            output[i + 3] = background.A;
        }
    }

    private static void CopyIntoOutput(
        byte[] output,
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        int targetWidth,
        int offsetX,
        int offsetY)
    {
        for (var y = 0; y < sourceHeight; y++)
        {
            var srcRow = y * sourceWidth * 4;
            var dstRow = (y + offsetY) * targetWidth * 4 + offsetX * 4;
            for (var x = 0; x < sourceWidth; x++)
            {
                var srcIdx = srcRow + x * 4;
                var dstIdx = dstRow + x * 4;
                output[dstIdx] = source[srcIdx];
                output[dstIdx + 1] = source[srcIdx + 1];
                output[dstIdx + 2] = source[srcIdx + 2];
                output[dstIdx + 3] = source[srcIdx + 3];
            }
        }
    }
}
