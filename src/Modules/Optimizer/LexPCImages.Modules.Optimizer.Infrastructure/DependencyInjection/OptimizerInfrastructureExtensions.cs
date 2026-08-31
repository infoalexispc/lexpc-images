using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;
using LexPCImages.Modules.Optimizer.Infrastructure.Configuration;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using LexPCImages.Modules.Optimizer.Infrastructure.Persistence;
using LexPCImages.Modules.Optimizer.Infrastructure.Queue;
using LexPCImages.Modules.Optimizer.Infrastructure.Registries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LexPCImages.Modules.Optimizer.Infrastructure.DependencyInjection;

public static class OptimizerInfrastructureExtensions
{
    public static IServiceCollection AddOptimizerInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<OptimizerOptions>()
            .Bind(configuration.GetSection(OptimizerOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptimizerPersistence();
        services.AddOptimizerImaging();
        services.AddOptimizerProcessing();

        return services;
    }

    private static void AddOptimizerPersistence(this IServiceCollection services)
    {
        services.AddSingleton<IJobRepository, InMemoryJobRepository>();
        services.AddSingleton<ISlotRegistry, SlotRegistry>();
    }

    private static void AddOptimizerImaging(this IServiceCollection services)
    {
        services.AddSingleton<IImageDecoder, ImageSharpDecoder>();
        services.AddSingleton<IImageEncoder, WebpImageEncoder>();
        services.AddSingleton<IImageResizer, ImageSharpResizer>();
        services.AddSingleton<IImagePadder, ImageSharpPadder>();
        services.AddSingleton<IImageTrimmer, AlphaBorderTrimmer>();
    }

    private static void AddOptimizerProcessing(this IServiceCollection services)
    {
        services.AddScoped<IJobProgressNotifier, JobProgressNotifier>();

        services.AddSingleton(provider => new ChannelJobQueue(
            provider.GetRequiredService<IOptions<OptimizerOptions>>().Value.QueueCapacity));
        services.AddSingleton<IJobQueueWriter>(provider => provider.GetRequiredService<ChannelJobQueue>());
        services.AddSingleton<IJobQueueReader>(provider => provider.GetRequiredService<ChannelJobQueue>());

        services.AddHostedService<ImageProcessingBackgroundService>();
    }
}
