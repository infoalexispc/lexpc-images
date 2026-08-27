using LexPCImages.Modules.Optimizer.Application.Abstractions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

public sealed class WebpEncoderService : IImageEncoder
{
    public async Task<byte[]> EncodeWebPAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sourceImage = new Image<Rgba32>(image.Width, image.Height);
        sourceImage.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                var sourceRowOffset = y * image.Width * 4;
                for (var x = 0; x < image.Width; x++)
                {
                    var i = sourceRowOffset + x * 4;
                    row[x] = new Rgba32(image.Rgba[i], image.Rgba[i + 1], image.Rgba[i + 2], image.Rgba[i + 3]);
                }
            }
        });

        await using var stream = new MemoryStream();
        var encoder = new WebpEncoder { Quality = 100, FileFormat = WebpFileFormatType.Lossless };
        await sourceImage.SaveAsync(stream, encoder, cancellationToken);
        return stream.ToArray();
    }
}
