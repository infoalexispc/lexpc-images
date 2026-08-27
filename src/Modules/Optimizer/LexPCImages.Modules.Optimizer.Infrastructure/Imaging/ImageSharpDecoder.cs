using LexPCImages.Modules.Optimizer.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class ImageSharpDecoder : IImageDecoder
{
    public async Task<DecodedImage> DecodeAsync(byte[] imageBytes, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = Image.Load<Rgba32>(imageBytes);
        var width = image.Width;
        var height = image.Height;
        var rgba = new byte[width * height * 4];
        image.CopyPixelDataTo(rgba);
        return new DecodedImage(width, height, rgba);
    }
}
