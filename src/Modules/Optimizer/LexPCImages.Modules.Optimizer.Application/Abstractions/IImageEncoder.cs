namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

/// <summary>Imagen ya serializada junto con el media type que la describe.</summary>
public sealed record EncodedImage(byte[] Content, string ContentType);

/// <summary>
/// Serializa una imagen decodificada. El formato concreto lo decide la implementación y viaja
/// en <see cref="EncodedImage.ContentType"/>, de modo que ni el caso de uso ni la capa web
/// necesitan conocerlo.
/// </summary>
public interface IImageEncoder
{
    Task<EncodedImage> EncodeAsync(DecodedImage image, CancellationToken cancellationToken);
}
