using System.Text;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using SixLabors.ImageSharp;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpDecoderTests
{
    [Fact]
    public async Task DecodeAsync_returns_width_height_and_rgba_bytes()
    {
        var bytes = CreateTestPng(64, 48);
        var decoder = new ImageSharpDecoder();

        var decoded = await decoder.DecodeAsync(bytes, CancellationToken.None);

        decoded.Width.Should().Be(64);
        decoded.Height.Should().Be(48);
        decoded.Rgba.Length.Should().Be(64 * 48 * 4);
        decoded.Rgba[0].Should().Be(255);
        decoded.Rgba[1].Should().Be(0);
        decoded.Rgba[2].Should().Be(0);
    }

    [Fact]
    public async Task DecodeAsync_supports_jpeg()
    {
        var bytes = CreateTestJpeg(8, 8);
        var decoder = new ImageSharpDecoder();

        var decoded = await decoder.DecodeAsync(bytes, CancellationToken.None);

        decoded.Width.Should().Be(8);
        decoded.Height.Should().Be(8);
    }

    private static byte[] CreateTestPng(int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    row[x] = new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 0, 0, 255);
                }
            }
        });
        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        return stream.ToArray();
    }

    private static byte[] CreateTestJpeg(int width, int height)
    {
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < accessor.Width; x++)
                {
                    row[x] = new SixLabors.ImageSharp.PixelFormats.Rgba32(0, 0, 255, 255);
                }
            }
        });
        using var stream = new MemoryStream();
        image.SaveAsJpeg(stream);
        return stream.ToArray();
    }
}
