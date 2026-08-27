using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpTightCropperTests
{
    [Fact]
    public void Crop_throws_when_dimensions_mismatch()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = new MaskResult(20, 20, new float[20 * 20]);

        var cropper = new ImageSharpTightCropper();
        var act = () => cropper.Crop(image, mask, 0.05);

        act.Should().Throw<InvalidOperationException>().WithMessage("*dimensions*");
    }

    [Fact]
    public void Crop_throws_when_mask_is_empty()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = new MaskResult(10, 10, new float[10 * 10]);

        var cropper = new ImageSharpTightCropper();
        var act = () => cropper.Crop(image, mask, 0.05);

        act.Should().Throw<InvalidOperationException>().WithMessage("*empty*");
    }

    [Fact]
    public void Crop_throws_when_marginPct_is_negative()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = MakeMask(10, 10, 2, 2, 7, 7);

        var cropper = new ImageSharpTightCropper();
        var act = () => cropper.Crop(image, mask, -0.1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Crop_throws_when_marginPct_is_greater_than_one()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = MakeMask(10, 10, 2, 2, 7, 7);

        var cropper = new ImageSharpTightCropper();
        var act = () => cropper.Crop(image, mask, 1.5);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Crop_returns_tightest_box_around_mask_with_zero_margin()
    {
        var (image, mask) = MakeScene(20, 20, 5, 5, 14, 14, 100);

        var cropper = new ImageSharpTightCropper();
        var result = cropper.Crop(image, mask, 0.0);

        result.Image.Width.Should().Be(10);
        result.Image.Height.Should().Be(10);
        result.Mask.Width.Should().Be(10);
        result.Mask.Height.Should().Be(10);
    }

    [Fact]
    public void Crop_applies_margin_percent()
    {
        var (image, mask) = MakeScene(40, 40, 10, 10, 29, 29, 100);

        var cropper = new ImageSharpTightCropper();
        var result = cropper.Crop(image, mask, 0.10);

        result.Image.Width.Should().Be(24);
        result.Image.Height.Should().Be(24);
    }

    [Fact]
    public void Crop_clamps_margin_at_image_edges()
    {
        var (image, mask) = MakeScene(20, 20, 0, 0, 19, 19, 100);

        var cropper = new ImageSharpTightCropper();
        var result = cropper.Crop(image, mask, 0.20);

        result.Image.Width.Should().Be(20);
        result.Image.Height.Should().Be(20);
    }

    [Fact]
    public void Crop_preserves_rgba_and_mask_values_in_cropped_region()
    {
        var (image, mask) = MakeScene(20, 20, 5, 5, 9, 9, 180);

        var cropper = new ImageSharpTightCropper();
        var result = cropper.Crop(image, mask, 0.0);

        for (var y = 0; y < 5; y++)
        {
            for (var x = 0; x < 5; x++)
            {
                var offset = (y * 5 + x) * 4;
                result.Image.Rgba[offset].Should().Be(180);
                result.Image.Rgba[offset + 3].Should().Be(255);
                result.Mask.Values[y * 5 + x].Should().Be(1f);
            }
        }
    }

    private static MaskResult MakeMask(int width, int height, int minX, int minY, int maxX, int maxY)
    {
        var values = new float[width * height];
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                values[y * width + x] = 1f;
            }
        }
        return new MaskResult(width, height, values);
    }

    private static (DecodedImage image, MaskResult mask) MakeScene(
        int width, int height, int minX, int minY, int maxX, int maxY, byte pixel)
    {
        var rgba = new byte[width * height * 4];
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var offset = (y * width + x) * 4;
                rgba[offset] = pixel;
                rgba[offset + 1] = pixel;
                rgba[offset + 2] = pixel;
                rgba[offset + 3] = 255;
            }
        }
        var mask = MakeMask(width, height, minX, minY, maxX, maxY);
        return (new DecodedImage(width, height, rgba), mask);
    }
}
