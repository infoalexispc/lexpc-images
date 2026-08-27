namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IImageEncoder
{
    Task<byte[]> EncodeWebPAsync(DecodedImage image, CancellationToken cancellationToken);
}
