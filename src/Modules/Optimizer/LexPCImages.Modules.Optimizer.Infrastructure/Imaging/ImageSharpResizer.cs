using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using AppResizeMode = LexPCImages.Modules.Optimizer.Application.Abstractions.ResizeMode;
using ImgResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpResizer : IImageResizer
{
    public Task<DecodedImage> ResizeAsync(
        DecodedImage source,
        int targetWidth,
        int targetHeight,
        AppResizeMode mode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceImage = RgbaImageInterop.ToImage(source);
        sourceImage.Mutate(context => context.Resize(BuildOptions(targetWidth, targetHeight, mode)));

        return Task.FromResult(RgbaImageInterop.ToDecodedImage(sourceImage));
    }

    private static ResizeOptions BuildOptions(int targetWidth, int targetHeight, AppResizeMode mode) => mode switch
    {
        AppResizeMode.FitWithTransparentPadding => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Pad,
            Sampler = KnownResamplers.Lanczos3,
            PadColor = Color.Transparent,
        },
        AppResizeMode.Stretch => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Stretch,
            Sampler = KnownResamplers.Lanczos3,
        },
        AppResizeMode.Cover => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Crop,
            Sampler = KnownResamplers.Lanczos3,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown resize mode."),
    };
}
