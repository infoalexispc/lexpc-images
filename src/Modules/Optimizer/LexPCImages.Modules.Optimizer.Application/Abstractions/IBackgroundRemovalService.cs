namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IBackgroundRemovalService
{
    Task<MaskResult> RemoveBackgroundAsync(DecodedImage image, CancellationToken cancellationToken);
}

public sealed record MaskResult(int Width, int Height, float[] Values);
