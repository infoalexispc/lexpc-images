using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using Microsoft.Extensions.Options;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpPadderTests
{
    [Fact]
    public void Pad_throws_when_target_dimensions_non_positive()
    {
        var image = MakeImage(100, 100, 200, 200, 200);
        var padder = CreatePadder();

        var act1 = () => padder.Pad(image, 0, 100);
        var act2 = () => padder.Pad(image, 100, -1);

        act1.Should().Throw<ArgumentOutOfRangeException>();
        act2.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Pad_outputs_target_dimensions()
    {
        var image = MakeImage(200, 100, 200, 200, 200);
        var padder = CreatePadder();

        var result = padder.Pad(image, 1000, 720);

        result.Image.Width.Should().Be(1000);
        result.Image.Height.Should().Be(720);
        result.Image.Rgba.Length.Should().Be(1000 * 720 * 4);
    }

    [Fact]
    public void Pad_fills_background_with_dominant_border_color()
    {
        var rgba = new byte[100 * 100 * 4];
        FillRect(rgba, 100, 100, 0, 0, 99, 99, 50, 50, 50);
        var image = new DecodedImage(100, 100, rgba);
        var padder = CreatePadder();

        var result = padder.Pad(image, 200, 200);

        result.Image.Rgba[0].Should().Be(50);
        result.Image.Rgba[1].Should().Be(50);
        result.Image.Rgba[2].Should().Be(50);
        result.Image.Rgba[3].Should().Be(255);
    }

    [Fact]
    public void Pad_centers_wider_image_with_vertical_padding()
    {
        var rgba = new byte[200 * 100 * 4];
        FillRect(rgba, 200, 100, 0, 0, 199, 99, 30, 30, 30);
        var image = new DecodedImage(200, 100, rgba);
        var padder = CreatePadder();

        var result = padder.Pad(image, 100, 100);

        result.Image.Width.Should().Be(100);
        result.Image.Height.Should().Be(100);
        result.OffsetX.Should().Be(0);
        result.OffsetY.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Pad_centers_taller_image_with_horizontal_padding()
    {
        var rgba = new byte[100 * 200 * 4];
        FillRect(rgba, 100, 200, 0, 0, 99, 199, 30, 30, 30);
        var image = new DecodedImage(100, 200, rgba);
        var padder = CreatePadder();

        var result = padder.Pad(image, 100, 100);

        result.Image.Width.Should().Be(100);
        result.Image.Height.Should().Be(100);
        result.OffsetX.Should().BeGreaterThan(0);
        result.OffsetY.Should().Be(0);
    }

    [Fact]
    public void Pad_centers_exact_aspect_image_with_no_padding()
    {
        var rgba = new byte[1000 * 720 * 4];
        FillRect(rgba, 1000, 720, 0, 0, 999, 719, 80, 80, 80);
        var image = new DecodedImage(1000, 720, rgba);
        var padder = CreatePadder();

        var result = padder.Pad(image, 1000, 720);

        result.Image.Width.Should().Be(1000);
        result.Image.Height.Should().Be(720);
        result.OffsetX.Should().Be(0);
        result.OffsetY.Should().Be(0);
    }

    private static DecodedImage MakeImage(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        FillRect(rgba, width, height, 0, 0, width - 1, height - 1, r, g, b);
        return new DecodedImage(width, height, rgba);
    }

    private static ImageSharpPadder CreatePadder(
        DownscaleFilter filter = DownscaleFilter.Box) =>
        new(Options.Create(new OptimizerOptions { DownscaleFilter = filter }));

    private static void FillRect(
        byte[] rgba,
        int width,
        int height,
        int minX,
        int minY,
        int maxX,
        int maxY,
        byte r,
        byte g,
        byte b)
    {
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var offset = (y * width + x) * 4;
                rgba[offset] = r;
                rgba[offset + 1] = g;
                rgba[offset + 2] = b;
                rgba[offset + 3] = 255;
            }
        }
    }
}
