using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;
using AppResizeMode = LexPCImages.Modules.Optimizer.Application.Abstractions.ResizeMode;
using ImgResizeMode = SixLabors.ImageSharp.Processing.ResizeMode;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpResizer : IImageResizer
{
    private readonly DownscaleFilter _filter;

    public ImageSharpResizer(IOptions<OptimizerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _filter = options.Value.DownscaleFilter;
    }

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

        var sampler = ResamplerSelector.For(
            _filter, source.Width, source.Height, targetWidth, targetHeight);

        using var sourceImage = RgbaImageInterop.ToImage(source);
        sourceImage.Mutate(context => context.Resize(BuildOptions(targetWidth, targetHeight, mode, sampler)));

        return Task.FromResult(RgbaImageInterop.ToDecodedImage(sourceImage));
    }

    private static ResizeOptions BuildOptions(
        int targetWidth, int targetHeight, AppResizeMode mode, IResampler sampler) => mode switch
    {
        AppResizeMode.FitWithTransparentPadding => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Pad,
            Sampler = sampler,
            PadColor = Color.Transparent,
        },
        AppResizeMode.Stretch => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Stretch,
            Sampler = sampler,
        },
        AppResizeMode.Cover => new ResizeOptions
        {
            Size = new Size(targetWidth, targetHeight),
            Mode = ImgResizeMode.Crop,
            Sampler = sampler,
        },
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown resize mode."),
    };
}
