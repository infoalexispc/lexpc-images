using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Validation;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp.Formats.Webp;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

/// <summary>
/// Codifica a WebP con la calidad configurada en <see cref="OptimizerOptions"/>.
/// <para>
/// El canal alfa viaja siempre sin pérdida, también en modo lossy: WebP comprime el plano alfa
/// aparte, de modo que la máscara de los recortes sale exacta y el borde no cría halos. Por eso
/// los slots que esperan imágenes sin fondo no necesitan un tratamiento distinto.
/// </para>
/// </summary>
public sealed class WebpImageEncoder : IImageEncoder
{
    /// <summary>Esfuerzo de compresión sin pérdida; en ese modo el parámetro no mide calidad visual.</summary>
    private const int LosslessEffort = 100;

    private readonly WebpEncoder _encoder;

    public WebpImageEncoder(IOptions<OptimizerOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var value = options.Value;
        _encoder = value.WebpLossless
            ? new WebpEncoder { FileFormat = WebpFileFormatType.Lossless, Quality = LosslessEffort }
            : new WebpEncoder { FileFormat = WebpFileFormatType.Lossy, Quality = value.WebpQuality };
    }

    public async Task<EncodedImage> EncodeAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        cancellationToken.ThrowIfCancellationRequested();

        using var sourceImage = RgbaImageInterop.ToImage(image);
        using var stream = new MemoryStream();
        await sourceImage.SaveAsync(stream, _encoder, cancellationToken);

        return new EncodedImage(stream.ToArray(), ImageContentTypes.Webp);
    }
}
