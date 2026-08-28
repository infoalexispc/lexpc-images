using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Application.Imaging;

/// <summary>Composición de una máscara de alfa sobre una imagen RGBA.</summary>
public static class MaskCompositor
{
    /// <summary>
    /// Multiplica el canal alfa de <paramref name="image"/> por los valores de <paramref name="mask"/>.
    /// Devuelve una imagen nueva; la original no se modifica.
    /// </summary>
    public static DecodedImage Apply(DecodedImage image, MaskResult mask)
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(mask);
        if (image.Width != mask.Width || image.Height != mask.Height)
        {
            throw new InvalidOperationException(
                $"Mask dimensions ({mask.Width}x{mask.Height}) do not match image ({image.Width}x{image.Height}).");
        }

        var rgba = new byte[image.Rgba.Length];
        image.Rgba.CopyTo(rgba, 0);
        for (var i = 0; i < mask.Values.Length; i++)
        {
            var alphaOffset = (i * 4) + 3;
            rgba[alphaOffset] = (byte)Math.Clamp(image.Rgba[alphaOffset] * mask.Values[i], 0f, 255f);
        }
        return new DecodedImage(image.Width, image.Height, rgba);
    }
}
