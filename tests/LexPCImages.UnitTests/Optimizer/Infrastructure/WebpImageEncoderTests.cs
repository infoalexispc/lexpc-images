using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class WebpImageEncoderTests
{
    private const int Width = 320;
    private const int Height = 315;

    [Fact]
    public async Task EncodeAsync_returns_webp_content_type()
    {
        var encoded = await Encode(new OptimizerOptions(), Photo());

        encoded.ContentType.Should().Be("image/webp");
        encoded.Content.Should().NotBeEmpty();
    }

    /// <summary>
    /// Que la configuración por defecto descarte información es justo el cambio que separa esta
    /// codificación de la anterior. Cuánto adelgaza el archivo depende de la imagen —el contenido
    /// sintético de este fixture se comprime mejor sin pérdida que con ella—, así que la relación
    /// de tamaños se mide sobre el catálogo real y aquí se afirma solo el modo.
    /// </summary>
    [Fact]
    public async Task EncodeAsync_loses_information_by_default()
    {
        var image = Photo();

        var encoded = await Encode(new OptimizerOptions(), image);

        Decode(encoded).Rgba.Should().NotEqual(image.Rgba);
    }

    [Theory]
    [InlineData(50, 75)]
    [InlineData(75, 90)]
    public async Task EncodeAsync_produces_smaller_files_as_quality_drops(int lower, int higher)
    {
        var image = Photo();

        var small = await Encode(new OptimizerOptions { WebpQuality = lower }, image);
        var large = await Encode(new OptimizerOptions { WebpQuality = higher }, image);

        small.Content.Length.Should().BeLessThan(large.Content.Length);
    }

    [Fact]
    public async Task EncodeAsync_without_loss_round_trips_every_pixel()
    {
        var image = Photo();

        var encoded = await Encode(new OptimizerOptions { WebpLossless = true }, image);

        Decode(encoded).Rgba.Should().Equal(image.Rgba);
    }

    /// <summary>
    /// El plano alfa se comprime aparte y sin pérdida, así que la máscara de los slots que reciben
    /// imágenes sin fondo sobrevive intacta a la codificación con pérdida. Sin esta garantía el
    /// recorte llegaría al catálogo con halos y habría que codificar esos slots sin pérdida.
    /// </summary>
    [Fact]
    public async Task EncodeAsync_preserves_the_alpha_channel_exactly_when_lossy()
    {
        var cutout = Cutout();

        var encoded = await Encode(new OptimizerOptions { WebpQuality = 75 }, cutout);

        Alpha(Decode(encoded)).Should().Equal(Alpha(cutout));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void WebpQuality_outside_the_webp_scale_is_rejected_on_start(int quality)
    {
        var options = new OptimizerOptions { WebpQuality = quality };

        Validator.TryValidateObject(options, new ValidationContext(options), null, validateAllProperties: true)
            .Should().BeFalse();
    }

    private static async Task<EncodedImage> Encode(OptimizerOptions options, DecodedImage image) =>
        await new WebpImageEncoder(Options.Create(options)).EncodeAsync(image, CancellationToken.None);

    private static DecodedImage Decode(EncodedImage encoded)
    {
        using var image = Image.Load<Rgba32>(encoded.Content);
        var rgba = new byte[image.Width * image.Height * 4];
        image.CopyPixelDataTo(rgba);
        return new DecodedImage(image.Width, image.Height, rgba);
    }

    private static byte[] Alpha(DecodedImage image)
    {
        var alpha = new byte[image.Rgba.Length / 4];
        for (var i = 0; i < alpha.Length; i++)
        {
            alpha[i] = image.Rgba[(i * 4) + 3];
        }
        return alpha;
    }

    /// <summary>Degradado con detalle fino: se comprime como una foto, no como un color plano.</summary>
    private static DecodedImage Photo()
    {
        var rgba = new byte[Width * Height * 4];
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var offset = ((y * Width) + x) * 4;
                rgba[offset] = (byte)((x * 255 / Width) ^ (y & 0x0F));
                rgba[offset + 1] = (byte)(y * 255 / Height);
                rgba[offset + 2] = (byte)((x + y) & 0xFF);
                rgba[offset + 3] = 255;
            }
        }
        return new DecodedImage(Width, Height, rgba);
    }

    /// <summary>Círculo opaco sobre fondo transparente, con borde suavizado como el de un recorte real.</summary>
    private static DecodedImage Cutout()
    {
        var source = Photo();
        var rgba = source.Rgba;
        var centerX = Width / 2.0;
        var centerY = Height / 2.0;
        var radius = Math.Min(Width, Height) / 3.0;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                var distance = Math.Sqrt(Math.Pow(x - centerX, 2) + Math.Pow(y - centerY, 2));
                var coverage = Math.Clamp(radius + 1 - distance, 0, 1);
                rgba[(((y * Width) + x) * 4) + 3] = (byte)Math.Round(coverage * 255);
            }
        }

        return source;
    }
}
