namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public sealed record DecodedImage(int Width, int Height, byte[] Rgba);

public interface IImageDecoder
{
    Task<DecodedImage> DecodeAsync(byte[] imageBytes, CancellationToken cancellationToken);
}
