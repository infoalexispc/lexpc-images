using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.IntegrationTests;

internal static class TestImages
{
    public static readonly Rgba32 Background = new(200, 50, 50, 255);
    public static readonly Rgba32 Marker = new(20, 220, 60, 255);

    public static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Background);
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// Fondo liso con dos marcas a media altura pegadas a los laterales, en el 5% exterior de cada
    /// lado. Permite distinguir el recorte del relleno: el recorte centrado se come las marcas y el
    /// relleno las conserva. Las marcas no llegan a las esquinas para no falsear la deteccion del
    /// color de fondo, que muestrea los cuatro bordes.
    /// </summary>
    public static byte[] PngWithSideMarkers(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, Background);
        var markerWidth = Math.Max(1, width * 5 / 100);
        var top = height * 40 / 100;
        var bottom = height * 60 / 100;

        image.ProcessPixelRows(accessor =>
        {
            for (var y = top; y < bottom; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < markerWidth; x++)
                {
                    row[x] = Marker;
                    row[width - 1 - x] = Marker;
                }
            }
        });

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
