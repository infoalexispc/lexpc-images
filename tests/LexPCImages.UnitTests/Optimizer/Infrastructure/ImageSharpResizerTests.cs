using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using Microsoft.Extensions.Options;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class ImageSharpResizerTests
{
    [Theory]
    [InlineData(ResizeMode.FitWithTransparentPadding)]
    [InlineData(ResizeMode.Stretch)]
    [InlineData(ResizeMode.Cover)]
    public async Task ResizeAsync_outputs_the_target_dimensions(ResizeMode mode)
    {
        var result = await Resize(Stripes(200, 160), 320, 315, mode);

        result.Width.Should().Be(320);
        result.Height.Should().Be(315);
        result.Rgba.Length.Should().Be(320 * 315 * 4);
    }

    /// <summary>
    /// El motivo de que el filtro sea configurable. Una trama más fina que dos píxeles de destino
    /// no cabe en la salida: Lanczos3 la elimina —es lo correcto— y deja una superficie lisa, que
    /// es como desaparecía la rejilla frontal de las cajas al bajar de 1600 px a 320. El promedio
    /// de área la deja pasar como muaré, y el ojo lee ese patrón como el detalle del producto.
    /// </summary>
    [Fact]
    public async Task ResizeAsync_with_Box_keeps_texture_that_Lanczos3_filters_out()
    {
        var source = Stripes(600, 40);

        var box = await Resize(source, 120, 40, ResizeMode.Stretch, DownscaleFilter.Box);
        var lanczos = await Resize(source, 120, 40, ResizeMode.Stretch, DownscaleFilter.Lanczos3);

        Contrast(lanczos).Should().BeLessThan(4, "Lanczos3 filtra la trama que no cabe en el destino");
        Contrast(box).Should().BeGreaterThan(Contrast(lanczos) * 3);
    }

    /// <summary>
    /// Al ampliar, el promedio de área degenera en vecino más próximo y devolvería bloques, así
    /// que el filtro configurado no manda: se interpola siempre con Lanczos3.
    /// </summary>
    [Fact]
    public async Task ResizeAsync_ignores_the_configured_filter_when_enlarging()
    {
        var source = Stripes(40, 40);

        var enlarged = await Resize(source, 320, 320, ResizeMode.Stretch, DownscaleFilter.Box);

        DistinctLevels(enlarged).Should().BeGreaterThan(
            DistinctLevels(source), "interpolar crea tonos intermedios; replicar píxeles no");
    }

    private static async Task<DecodedImage> Resize(
        DecodedImage source,
        int width,
        int height,
        ResizeMode mode,
        DownscaleFilter filter = DownscaleFilter.Box)
    {
        var options = Options.Create(new OptimizerOptions { DownscaleFilter = filter });
        return await new ImageSharpResizer(options)
            .ResizeAsync(source, width, height, mode, CancellationToken.None);
    }

    /// <summary>
    /// Franjas verticales de periodo 4 px: la trama fina que se pierde al reducir. El periodo no
    /// divide a la ventana del promedio de área en las reducciones de los tests, que es la
    /// condición para que la trama reaparezca como muaré en lugar de cancelarse.
    /// </summary>
    private static DecodedImage Stripes(int width, int height)
    {
        var rgba = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var offset = ((y * width) + x) * 4;
                var value = (byte)(x % 4 < 2 ? 20 : 235);
                rgba[offset] = value;
                rgba[offset + 1] = value;
                rgba[offset + 2] = value;
                rgba[offset + 3] = 255;
            }
        }
        return new DecodedImage(width, height, rgba);
    }

    /// <summary>Desviación típica del canal rojo: cuánta variación sobrevive al remuestreo.</summary>
    private static double Contrast(DecodedImage image)
    {
        var values = Reds(image);
        var mean = values.Average();
        return Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Count);
    }

    private static int DistinctLevels(DecodedImage image) => Reds(image).Distinct().Count();

    private static List<double> Reds(DecodedImage image)
    {
        var values = new List<double>(image.Rgba.Length / 4);
        for (var i = 0; i < image.Rgba.Length; i += 4)
        {
            values.Add(image.Rgba[i]);
        }
        return values;
    }
}
