using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.IntegrationTests;

internal static class TestImages
{
    public static byte[] Png(int width, int height)
    {
        using var image = new Image<Rgba32>(width, height, new Rgba32(200, 50, 50, 255));
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }
}
