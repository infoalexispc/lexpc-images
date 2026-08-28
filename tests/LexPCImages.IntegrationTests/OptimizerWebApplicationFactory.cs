using LexPCImages.Modules.Optimizer.Application.Abstractions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace LexPCImages.IntegrationTests;

/// <summary>
/// Host de pruebas con el modelo ONNX sustituido por un doble: el pipeline se ejerce completo
/// sin depender del fichero de 84 MB ni del tiempo de inferencia.
/// </summary>
public sealed class OptimizerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor
                .Singleton<IBackgroundRemovalService, FakeBackgroundRemovalService>()));
    }
}

/// <summary>Genera una máscara circular centrada, suficiente para recortar y componer.</summary>
internal sealed class FakeBackgroundRemovalService : IBackgroundRemovalService
{
    public Task<MaskResult> RemoveBackgroundAsync(DecodedImage image, CancellationToken cancellationToken)
    {
        var mask = new float[image.Width * image.Height];
        var centerX = image.Width / 2f;
        var centerY = image.Height / 2f;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var dx = (x - centerX) / centerX;
                var dy = (y - centerY) / centerY;
                mask[(y * image.Width) + x] = (dx * dx) + (dy * dy) < 0.8f ? 1f : 0f;
            }
        }

        return Task.FromResult(new MaskResult(image.Width, image.Height, mask));
    }
}
