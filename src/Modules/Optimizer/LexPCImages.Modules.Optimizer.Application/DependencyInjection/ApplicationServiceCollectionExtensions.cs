using LexPCImages.Modules.Optimizer.Application.Pipelines;
using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobDownload;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using Microsoft.Extensions.DependencyInjection;

namespace LexPCImages.Modules.Optimizer.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddOptimizerApplication(this IServiceCollection services)
    {
        services.TryAddTimeProvider();

        services.AddScoped<EnqueueJobHandler>();
        services.AddScoped<GetJobStatusHandler>();
        services.AddScoped<GetJobDownloadHandler>();
        services.AddScoped<ProcessImageHandler>();

        // Una estrategia por SlotMode: añadir un modo nuevo es añadir una línea aquí.
        services.AddScoped<IImageProcessingPipeline, BackgroundRemovalPipeline>();
        services.AddScoped<IImageProcessingPipeline, ResizeAndPadPipeline>();

        return services;
    }

    private static void TryAddTimeProvider(this IServiceCollection services)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
    }
}
