using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Validation;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using SixLabors.ImageSharp.Formats.Webp;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

/// <summary>Codifica a WebP sin pérdida, conservando el canal alfa del recorte.</summary>
public sealed class WebpImageEncoder : IImageEncoder
{
    private static readonly WebpEncoder Encoder = new()
    {
        Quality = 100,
        FileFormat = WebpFileFormatType.Lossless,
    };

    public async Task<EncodedImage> EncodeAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceImage = RgbaImageInterop.ToImage(image);
        using var stream = new MemoryStream();
        await sourceImage.SaveAsync(stream, Encoder, cancellationToken);

        return new EncodedImage(stream.ToArray(), ImageContentTypes.Webp);
    }
}
