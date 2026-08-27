using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpLegProtectorTests
{
    [Fact]
    public void Protect_throws_when_dimensions_mismatch()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = new MaskResult(20, 20, new float[20 * 20]);

        var protector = new ImageSharpLegProtector();
        var act = () => protector.Protect(image, mask);

        act.Should().Throw<InvalidOperationException>().WithMessage("*dimensions*");
    }

    [Fact]
    public void Protect_recovers_thin_leg_at_alpha_with_visible_content()
    {
        var (image, mask) = MakeLegScene(40, 40, legMinX: 18, legMaxX: 21, legMinY: 5, legMaxY: 34);

        var protector = new ImageSharpLegProtector();
        var result = protector.Protect(image, mask);

        for (var y = 5; y <= 34; y++)
        {
            for (var x = 18; x <= 21; x++)
            {
                result.Values[y * 40 + x].Should().Be(1f, $"pixel ({x},{y}) of leg");
            }
        }
    }

    [Fact]
    public void Protect_does_not_recover_dark_regions()
    {
        var rgba = new byte[40 * 40 * 4];
        var maskValues = new float[40 * 40];
        for (var y = 5; y <= 34; y++)
        {
            for (var x = 18; x <= 21; x++)
            {
                maskValues[y * 40 + x] = 0.2f;
                var offset = (y * 40 + x) * 4;
                rgba[offset] = 20;
                rgba[offset + 1] = 20;
                rgba[offset + 2] = 20;
            }
        }
        var image = new DecodedImage(40, 40, rgba);
        var mask = new MaskResult(40, 40, maskValues);

        var protector = new ImageSharpLegProtector();
        var result = protector.Protect(image, mask);

        for (var y = 5; y <= 34; y++)
        {
            for (var x = 18; x <= 21; x++)
            {
                result.Values[y * 40 + x].Should().Be(0f, $"pixel ({x},{y}) dark, not recovered");
            }
        }
    }

    [Fact]
    public void Protect_preserves_strong_mask_regions()
    {
        var (image, mask) = MakeLegScene(40, 40, legMinX: 18, legMaxX: 21, legMinY: 5, legMaxY: 34);
        FillRect(mask.Values, 40, 40, 15, 5, 24, 34, 1f);

        var protector = new ImageSharpLegProtector();
        var result = protector.Protect(image, mask);

        for (var y = 5; y <= 34; y++)
        {
            for (var x = 15; x <= 24; x++)
            {
                result.Values[y * 40 + x].Should().Be(1f, $"pixel ({x},{y})");
            }
        }
    }

    [Fact]
    public void Protect_returns_binary_mask()
    {
        var (image, mask) = MakeLegScene(40, 40, legMinX: 18, legMaxX: 21, legMinY: 5, legMaxY: 34);

        var protector = new ImageSharpLegProtector();
        var result = protector.Protect(image, mask);

        foreach (var v in result.Values)
        {
            (v == 0f || v == 1f).Should().BeTrue();
        }
    }

    [Fact]
    public void Protect_does_not_recover_thin_horizontal_artifacts()
    {
        var rgba = new byte[40 * 40 * 4];
        var maskValues = new float[40 * 40];
        for (var x = 5; x <= 34; x++)
        {
            maskValues[20 * 40 + x] = 0.2f;
            var offset = (20 * 40 + x) * 4;
            rgba[offset] = 200;
            rgba[offset + 1] = 200;
            rgba[offset + 2] = 200;
        }
        var image = new DecodedImage(40, 40, rgba);
        var mask = new MaskResult(40, 40, maskValues);

        var protector = new ImageSharpLegProtector();
        var result = protector.Protect(image, mask);

        for (var x = 5; x <= 34; x++)
        {
            result.Values[20 * 40 + x].Should().Be(0f, $"horizontal pixel ({x},20)");
        }
    }

    private static (DecodedImage image, MaskResult mask) MakeLegScene(int width, int height, int legMinX, int legMaxX, int legMinY, int legMaxY)
    {
        var rgba = new byte[width * height * 4];
        var maskValues = new float[width * height];
        for (var y = legMinY; y <= legMaxY; y++)
        {
            for (var x = legMinX; x <= legMaxX; x++)
            {
                maskValues[y * width + x] = 0.2f;
                var offset = (y * width + x) * 4;
                rgba[offset] = 200;
                rgba[offset + 1] = 200;
                rgba[offset + 2] = 200;
            }
        }
        return (new DecodedImage(width, height, rgba), new MaskResult(width, height, maskValues));
    }

    private static void FillRect(float[] values, int width, int height, int minX, int minY, int maxX, int maxY, float value)
    {
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                values[y * width + x] = value;
            }
        }
    }
}
