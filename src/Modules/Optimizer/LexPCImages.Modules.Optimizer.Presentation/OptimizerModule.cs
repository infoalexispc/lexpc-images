using LexPCImages.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LexPCImages.Modules.Optimizer.Presentation;

public sealed class OptimizerModule : IModuleRegistration
{
    public string Name => "Optimizer";

    public IServiceCollection RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    public IEndpointRouteBuilder MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/optimizer").WithTags("Optimizer");

        group.MapGet("/health", () => Results.Ok(new { module = Name, status = "ok" }))
            .WithName("OptimizerHealth");

        return endpoints;
    }
}
