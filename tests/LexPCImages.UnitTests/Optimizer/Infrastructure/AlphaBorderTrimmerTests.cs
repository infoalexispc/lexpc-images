using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class AlphaBorderTrimmerTests
{
    private readonly AlphaBorderTrimmer _trimmer = new();

    [Fact]
    public void TrimTransparentBorder_reduces_the_canvas_to_the_visible_content()
    {
        var image = WithOpaqueRect(width: 100, height: 80, left: 10, top: 20, right: 59, bottom: 49);

        var trimmed = _trimmer.TrimTransparentBorder(image);

        trimmed.Width.Should().Be(50);
        trimmed.Height.Should().Be(30);
        trimmed.Rgba.Length.Should().Be(50 * 30 * 4);
    }

    [Fact]
    public void TrimTransparentBorder_keeps_the_pixels_it_did_not_cut()
    {
        var image = WithOpaqueRect(width: 20, height: 20, left: 4, top: 6, right: 9, bottom: 11);

        var trimmed = _trimmer.TrimTransparentBorder(image);

        for (var i = 0; i < trimmed.Rgba.Length; i += 4)
        {
            trimmed.Rgba[i].Should().Be(200);
            trimmed.Rgba[i + 3].Should().Be(255);
        }
    }

    /// <summary>
    /// Un borde difuminado es contenido, no marco: el umbral es "algo de opacidad", así que la
    /// media sombra de un recorte sobrevive al recorte del lienzo.
    /// </summary>
    [Fact]
    public void TrimTransparentBorder_keeps_partially_transparent_pixels()
    {
        var image = WithOpaqueRect(width: 20, height: 20, left: 8, top: 8, right: 11, bottom: 11);
        SetAlpha(image, x: 2, y: 3, alpha: 1);

        var trimmed = _trimmer.TrimTransparentBorder(image);

        trimmed.Width.Should().Be(10);
        trimmed.Height.Should().Be(9);
    }

    [Fact]
    public void TrimTransparentBorder_returns_the_same_image_when_nothing_is_transparent()
    {
        var image = WithOpaqueRect(width: 12, height: 12, left: 0, top: 0, right: 11, bottom: 11);

        _trimmer.TrimTransparentBorder(image).Should().BeSameAs(image);
    }

    /// <summary>Sin un solo píxel opaco no hay contenido que encuadrar: se devuelve tal cual.</summary>
    [Fact]
    public void TrimTransparentBorder_returns_the_same_image_when_everything_is_transparent()
    {
        var image = new DecodedImage(8, 8, new byte[8 * 8 * 4]);

        _trimmer.TrimTransparentBorder(image).Should().BeSameAs(image);
    }

    [Fact]
    public void TrimTransparentBorder_rejects_a_null_image()
    {
        var act = () => _trimmer.TrimTransparentBorder(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    private static DecodedImage WithOpaqueRect(
        int width, int height, int left, int top, int right, int bottom)
    {
        var rgba = new byte[width * height * 4];
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                var offset = ((y * width) + x) * 4;
                rgba[offset] = 200;
                rgba[offset + 1] = 100;
                rgba[offset + 2] = 50;
                rgba[offset + 3] = 255;
            }
        }
        return new DecodedImage(width, height, rgba);
    }

    private static void SetAlpha(DecodedImage image, int x, int y, byte alpha)
    {
        image.Rgba[((((y * image.Width) + x) * 4) + 3)] = alpha;
    }
}
