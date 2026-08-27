using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using Microsoft.Extensions.DependencyInjection;

namespace LexPCImages.Modules.Optimizer.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddOptimizerApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<EnqueueJobHandler>();
        services.AddScoped<GetJobStatusHandler>();
        services.AddScoped<ProcessImageHandler>();
        return services;
    }
}
