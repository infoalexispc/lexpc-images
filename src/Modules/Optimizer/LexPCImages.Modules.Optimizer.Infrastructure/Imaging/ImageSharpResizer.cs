using LexPCImages.Modules.Optimizer.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AppResizeMode = LexPCImages.Modules.Optimizer.Application.Abstractions.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpResizer : IImageResizer
{
    public async Task<DecodedImage> ResizeAsync(
        DecodedImage source,
        int targetWidth,
        int targetHeight,
        AppResizeMode mode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceImage = WrapRgba(source.Rgba, source.Width, source.Height);
        var options = BuildOptions(targetWidth, targetHeight, mode);

        sourceImage.Mutate(x => x.Resize(options));

        var rgba = new byte[sourceImage.Width * sourceImage.Height * 4];
        sourceImage.CopyPixelDataTo(rgba);
        return new DecodedImage(sourceImage.Width, sourceImage.Height, rgba);
    }

    private static Image<Rgba32> WrapRgba(byte[] rgba, int width, int height)
    {
        var image = new Image<Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var sourceRowOffset = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    var i = sourceRowOffset + x * 4;
                    row[x] = new Rgba32(rgba[i], rgba[i + 1], rgba[i + 2], rgba[i + 3]);
                }
            }
        });
        return image;
    }

    private static ResizeOptions BuildOptions(int targetWidth, int targetHeight, AppResizeMode mode) => mode switch
    {
        AppResizeMode.FitWithTransparentPadding => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Pad,
            Sampler = KnownResamplers.Lanczos3,
            PadColor = Color.Transparent,
        },
        AppResizeMode.Stretch => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        },
        AppResizeMode.Cover => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = SixLabors.ImageSharp.Processing.ResizeMode.Crop,
            Sampler = KnownResamplers.Lanczos3,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };
}
