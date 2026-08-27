using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpShadowSuppressorTests
{
    [Fact]
    public void Suppress_throws_when_dimensions_mismatch()
    {
        var image = new DecodedImage(10, 10, new byte[10 * 10 * 4]);
        var mask = new MaskResult(20, 20, new float[20 * 20]);

        var suppressor = new ImageSharpShadowSuppressor();
        var act = () => suppressor.Suppress(image, mask);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*dimensions*");
    }

    [Fact]
    public void Suppress_does_not_touch_pixels_outside_mask()
    {
        var image = MakeImage(20, 20, r: 140, g: 140, b: 140);
        var maskValues = new float[20 * 20];
        for (var i = 0; i < maskValues.Length; i++)
        {
            maskValues[i] = 0.05f;
        }
        var mask = new MaskResult(20, 20, maskValues);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0.05f));
    }

    [Fact]
    public void Suppress_preserves_pixels_with_high_alpha_even_if_shadow_like()
    {
        var image = MakeImage(20, 20, r: 140, g: 140, b: 140);
        var mask = FullMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(1f));
    }

    [Fact]
    public void Suppress_does_nothing_when_image_is_too_bright()
    {
        var image = MakeImage(20, 20, r: 200, g: 200, b: 200);
        var mask = WeakMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0.3f));
    }

    [Fact]
    public void Suppress_does_nothing_when_image_is_too_dark()
    {
        var image = MakeImage(20, 20, r: 20, g: 20, b: 20);
        var mask = WeakMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0.3f));
    }

    [Fact]
    public void Suppress_does_nothing_when_pixels_are_saturated()
    {
        var image = MakeImage(20, 20, r: 200, g: 50, b: 50);
        var mask = WeakMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0.3f));
    }

    [Fact]
    public void Suppress_fully_removes_cast_shadow_outside_object_context()
    {
        var rgba = new byte[20 * 20 * 4];
        var maskValues = new float[20 * 20];
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var offset = (y * 20 + x) * 4;
                rgba[offset] = 140;
                rgba[offset + 1] = 140;
                rgba[offset + 2] = 140;
                rgba[offset + 3] = 255;
                maskValues[y * 20 + x] = 0.4f;
            }
        }
        var image = new DecodedImage(20, 20, rgba);
        var mask = new MaskResult(20, 20, maskValues);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        for (var y = 5; y < 19; y++)
        {
            for (var x = 5; x < 19; x++)
            {
                result.Values[y * 20 + x].Should().Be(0f, $"pixel ({x},{y}) in cast shadow zone");
            }
        }
    }

    [Fact]
    public void Suppress_keeps_alpha_on_bright_half_and_reduces_on_shadow_half()
    {
        var rgba = new byte[20 * 20 * 4];
        var maskValues = new float[20 * 20];
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var offset = (y * 20 + x) * 4;
                if (x < 10)
                {
                    rgba[offset] = 200;
                    rgba[offset + 1] = 200;
                    rgba[offset + 2] = 200;
                }
                else
                {
                    rgba[offset] = 140;
                    rgba[offset + 1] = 140;
                    rgba[offset + 2] = 140;
                }
                rgba[offset + 3] = 255;
                maskValues[y * 20 + x] = 0.3f;
            }
        }
        var image = new DecodedImage(20, 20, rgba);
        var mask = new MaskResult(20, 20, maskValues);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                result.Values[y * 20 + x].Should().Be(0.3f,
                    $"pixel ({x},{y}) bright color, not shadow-like");
            }
        }
        for (var y = 0; y < 20; y++)
        {
            for (var x = 11; x < 19; x++)
            {
                result.Values[y * 20 + x].Should().Be(0f,
                    $"pixel ({x},{y}) in shadow zone (cast shadow)");
            }
        }
    }


    [Fact]
    public void Suppress_attenuates_shadow_color_when_local_alpha_is_low()
    {
        var image = MakeImage(20, 20, r: 140, g: 140, b: 140);
        var mask = WeakMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void Suppress_keeps_alpha_when_image_is_not_shadow_like_even_with_weak_mask()
    {
        var image = MakeImage(20, 20, r: 200, g: 50, b: 50);
        var mask = WeakMask(20, 20);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0.3f));
    }

    [Fact]
    public void Suppress_attacks_cast_shadow_outside_object_when_mask_is_uniform_weak()
    {
        var image = MakeImage(20, 20, r: 140, g: 140, b: 140);
        var mask = new MaskResult(20, 20, Enumerable.Repeat(0.3f, 20 * 20).ToArray());

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    [Fact]
    public void Suppress_partially_attenuates_form_shadow_inside_object()
    {
        var rgba = new byte[20 * 20 * 4];
        var maskValues = new float[20 * 20];
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                var offset = (y * 20 + x) * 4;
                rgba[offset] = 140;
                rgba[offset + 1] = 140;
                rgba[offset + 2] = 140;
                rgba[offset + 3] = 255;
                maskValues[y * 20 + x] = 0.4f;
            }
        }
        var image = new DecodedImage(20, 20, rgba);
        var mask = new MaskResult(20, 20, maskValues);

        var suppressor = new ImageSharpShadowSuppressor();
        var result = suppressor.Suppress(image, mask);

        result.Values.Should().AllSatisfy(v => v.Should().Be(0f));
    }

    private static DecodedImage MakeImage(int width, int height, byte r, byte g, byte b)
    {
        var rgba = new byte[width * height * 4];
        for (var i = 0; i < rgba.Length; i += 4)
        {
            rgba[i] = r;
            rgba[i + 1] = g;
            rgba[i + 2] = b;
            rgba[i + 3] = 255;
        }
        return new DecodedImage(width, height, rgba);
    }

    private static MaskResult FullMask(int width, int height) =>
        new(width, height, Enumerable.Repeat(1f, width * height).ToArray());

    private static MaskResult WeakMask(int width, int height) =>
        new(width, height, Enumerable.Repeat(0.3f, width * height).ToArray());
}
