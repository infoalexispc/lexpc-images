using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace LexPCImages.Modules.Optimizer.Presentation;

/// <summary>
/// Puntos de enganche del módulo con el host. El composition root no necesita conocer los tipos
/// internos del módulo —antes referenciaba el controlador por reflexión— sino solo estos métodos.
/// </summary>
public static class OptimizerModule
{
    public static IMvcBuilder AddOptimizerPresentation(this IMvcBuilder mvc)
    {
        ArgumentNullException.ThrowIfNull(mvc);
        return mvc.AddApplicationPart(typeof(OptimizerModule).Assembly);
    }

    public static IEndpointRouteBuilder MapOptimizerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/optimizer").WithTags("Optimizer");

        group.MapGet("/health", () => Results.Ok(new { module = "Optimizer", status = "ok" }))
            .WithName("OptimizerHealth");

        return endpoints;
    }
}
