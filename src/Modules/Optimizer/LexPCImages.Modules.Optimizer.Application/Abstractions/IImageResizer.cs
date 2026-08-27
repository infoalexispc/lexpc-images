namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IImageResizer
{
    Task<DecodedImage> ResizeAsync(
        DecodedImage source,
        int targetWidth,
        int targetHeight,
        ResizeMode mode,
        CancellationToken cancellationToken);
}

public enum ResizeMode
{
    FitWithTransparentPadding,
    Stretch,
    Cover,
}
