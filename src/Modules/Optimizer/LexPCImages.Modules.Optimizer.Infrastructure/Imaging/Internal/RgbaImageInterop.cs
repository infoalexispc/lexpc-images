using LexPCImages.Modules.Optimizer.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging.Internal;

/// <summary>
/// Conversión entre <see cref="DecodedImage"/> (RGBA plano) e <see cref="Image{Rgba32}"/>.
/// Antes cada servicio repetía su propio bucle píxel a píxel; aquí se hace con una copia de
/// memoria, porque <c>Rgba32</c> tiene exactamente la misma disposición que los 4 bytes RGBA.
/// </summary>
internal static class RgbaImageInterop
{
    public const int BytesPerPixel = 4;

    public static Image<Rgba32> ToImage(DecodedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);
        EnsureBufferMatchesDimensions(image);

        return Image.LoadPixelData<Rgba32>(image.Rgba, image.Width, image.Height);
    }

    public static DecodedImage ToDecodedImage(Image<Rgba32> image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var rgba = new byte[(long)image.Width * image.Height * BytesPerPixel];
        image.CopyPixelDataTo(rgba);
        return new DecodedImage(image.Width, image.Height, rgba);
    }

    private static void EnsureBufferMatchesDimensions(DecodedImage image)
    {
        var expected = (long)image.Width * image.Height * BytesPerPixel;
        if (image.Rgba.LongLength != expected)
        {
            throw new ArgumentException(
                $"RGBA buffer of {image.Rgba.LongLength} bytes does not match {image.Width}x{image.Height} " +
                $"({expected} bytes expected).",
                nameof(image));
        }
    }
}
