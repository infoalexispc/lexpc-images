using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Application;

public sealed class MaskCompositorTests
{
    private static DecodedImage OpaqueImage(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        Array.Fill(rgba, (byte)255);
        return new DecodedImage(width, height, rgba);
    }

    [Fact]
    public void Apply_multiplies_the_alpha_channel_by_the_mask()
    {
        var image = OpaqueImage(2, 1);
        var mask = new MaskResult(2, 1, [1f, 0f]);

        var result = MaskCompositor.Apply(image, mask);

        result.Rgba[3].Should().Be(255);
        result.Rgba[7].Should().Be(0);
    }

    [Fact]
    public void Apply_keeps_the_colour_channels_untouched()
    {
        var image = new DecodedImage(1, 1, [10, 20, 30, 200]);
        var mask = new MaskResult(1, 1, [0.5f]);

        var result = MaskCompositor.Apply(image, mask);

        result.Rgba[0].Should().Be(10);
        result.Rgba[1].Should().Be(20);
        result.Rgba[2].Should().Be(30);
        result.Rgba[3].Should().Be(100);
    }

    [Fact]
    public void Apply_does_not_mutate_the_source_image()
    {
        var image = OpaqueImage(1, 1);
        var mask = new MaskResult(1, 1, [0f]);

        MaskCompositor.Apply(image, mask);

        image.Rgba[3].Should().Be(255);
    }

    [Fact]
    public void Apply_rejects_a_mask_with_different_dimensions()
    {
        var image = OpaqueImage(2, 2);
        var mask = new MaskResult(1, 1, [1f]);

        var act = () => MaskCompositor.Apply(image, mask);

        act.Should().Throw<InvalidOperationException>().WithMessage("*do not match*");
    }
}
