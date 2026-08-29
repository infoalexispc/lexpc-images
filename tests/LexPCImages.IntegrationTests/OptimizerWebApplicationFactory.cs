using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LexPCImages.IntegrationTests;

/// <summary>
/// Host de pruebas. Ya no sustituye nada: al desaparecer la segmentación no queda ninguna
/// dependencia pesada, así que los tests ejercen exactamente los servicios de producción.
/// </summary>
public sealed class OptimizerWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
