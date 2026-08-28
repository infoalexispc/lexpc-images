namespace LexPCImages.Modules.Optimizer.Application.Validation;

/// <summary>
/// Formatos de entrada admitidos. Además de la cabecera <c>Content-Type</c> —que la envía el
/// cliente y por tanto no es de fiar— se comprueba la firma real de los bytes.
/// </summary>
public static class ImageContentTypes
{
    public const string Jpeg = "image/jpeg";
    public const string Png = "image/png";
    public const string Webp = "image/webp";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        Jpeg,
        Png,
        Webp,
    };

    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] RiffSignature = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WebpSignature = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    public static bool IsAllowed(string? contentType) =>
        !string.IsNullOrWhiteSpace(contentType) && Allowed.Contains(contentType);

    /// <summary>
    /// Comprueba que los bytes empiezan realmente por la firma de un formato admitido.
    /// Evita que un fichero arbitrario entre en el pipeline solo por declarar un media type válido.
    /// </summary>
    public static bool HasSupportedSignature(ReadOnlySpan<byte> imageBytes) =>
        DetectContentType(imageBytes) is not null;

    /// <summary>Devuelve el media type deducido de los bytes, o <c>null</c> si no es un formato admitido.</summary>
    public static string? DetectContentType(ReadOnlySpan<byte> imageBytes)
    {
        if (imageBytes.StartsWith(PngSignature))
        {
            return Png;
        }
        if (imageBytes.StartsWith(JpegSignature))
        {
            return Jpeg;
        }
        // WebP: "RIFF" + 4 bytes de tamaño + "WEBP".
        if (imageBytes.Length >= 12
            && imageBytes.StartsWith(RiffSignature)
            && imageBytes.Slice(8, 4).SequenceEqual(WebpSignature))
        {
            return Webp;
        }
        return null;
    }
}
