using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

/// <summary>
/// Recorta el marco transparente recorriendo el canal alfa del búfer RGBA. No necesita ImageSharp:
/// la operación es una búsqueda de mínimos y máximos más una copia de filas.
/// <para>
/// El umbral es "alfa mayor que cero" y no un valor holgado a propósito: un borde difuminado o la
/// sombra suave de un recorte son contenido, y subir el umbral se los comería sin avisar.
/// </para>
/// </summary>
public sealed class AlphaBorderTrimmer : IImageTrimmer
{
    private const int AlphaOffset = 3;

    public DecodedImage TrimTransparentBorder(DecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (!TryFindContentBounds(image, out var left, out var top, out var right, out var bottom))
        {
            return image;
        }

        if (left == 0 && top == 0 && right == image.Width - 1 && bottom == image.Height - 1)
        {
            return image;
        }

        return Crop(image, left, top, right - left + 1, bottom - top + 1);
    }

    /// <summary>
    /// <see langword="false"/> cuando no hay un solo píxel opaco: una imagen enteramente
    /// transparente no tiene contenido que encuadrar y se devuelve tal cual.
    /// </summary>
    private static bool TryFindContentBounds(
        DecodedImage image, out int left, out int top, out int right, out int bottom)
    {
        left = image.Width;
        top = image.Height;
        right = -1;
        bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            var rowOffset = y * image.Width * RgbaImageInterop.BytesPerPixel;
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Rgba[rowOffset + (x * RgbaImageInterop.BytesPerPixel) + AlphaOffset] == 0)
                {
                    continue;
                }

                if (x < left)
                {
                    left = x;
                }
                if (x > right)
                {
                    right = x;
                }
                if (y < top)
                {
                    top = y;
                }
                bottom = y;
            }
        }

        return right >= 0;
    }

    private static DecodedImage Crop(DecodedImage source, int left, int top, int width, int height)
    {
        var rowBytes = width * RgbaImageInterop.BytesPerPixel;
        var cropped = new byte[(long)rowBytes * height];

        for (var y = 0; y < height; y++)
        {
            var sourceOffset = (((y + top) * source.Width) + left) * RgbaImageInterop.BytesPerPixel;
            Array.Copy(source.Rgba, sourceOffset, cropped, (long)y * rowBytes, rowBytes);
        }

        return new DecodedImage(width, height, cropped);
    }
}
