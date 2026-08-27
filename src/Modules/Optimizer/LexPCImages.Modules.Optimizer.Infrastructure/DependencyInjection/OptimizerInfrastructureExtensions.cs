using System.Threading.Channels;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Infrastructure.Ai;
using LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;
using LexPCImages.Modules.Optimizer.Infrastructure.Imaging;
using LexPCImages.Modules.Optimizer.Infrastructure.Persistence;
using LexPCImages.Modules.Optimizer.Infrastructure.Registries;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LexPCImages.Modules.Optimizer.Infrastructure.DependencyInjection;

public static class OptimizerInfrastructureExtensions
{
    public static IServiceCollection AddOptimizerInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var modelPath = ResolveModelPath(configuration["Optimizer:ModelPath"]);

        services.AddSingleton<IJobRepository, InMemoryJobRepository>();
        services.AddSingleton<ISlotRegistry, SlotRegistry>();

        services.AddSingleton<IImageDecoder, ImageSharpDecoder>();
        services.AddSingleton<IImageResizer, ImageSharpResizer>();
        services.AddSingleton<IImageEncoder, WebpEncoderService>();
        services.AddSingleton<IShadowSuppressor, ImageSharpShadowSuppressor>();
        services.AddSingleton<IDeskMaskRefiner, ImageSharpDeskMaskRefiner>();
        services.AddSingleton<ILegProtector, ImageSharpLegProtector>();
        services.AddSingleton<ITightCropper, ImageSharpTightCropper>();
        services.AddSingleton<IBackgroundRemovalService>(_ => new OnnxBackgroundRemovalService(modelPath));

        services.AddScoped<IJobProgressNotifier, JobProgressNotifier>();

        services.AddSingleton(_ => Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false,
        }));
        services.AddSingleton<ImageProcessingBackgroundService>();
        services.AddHostedService(sp => sp.GetRequiredService<ImageProcessingBackgroundService>());

        return services;
    }

    public static IServiceCollection AddOptimizerQueueWriter(this IServiceCollection services)
    {
        return services.AddSingleton<OptimizerQueueWriter>();
    }

    private static string ResolveModelPath(string? configured)
    {
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.Combine(AppContext.BaseDirectory, "models", "rmbg-1.4-fp16.onnx");
        }
        if (Path.IsPathRooted(configured))
        {
            return configured;
        }
        return Path.Combine(AppContext.BaseDirectory, configured);
    }
}

public sealed class OptimizerQueueWriter
{
    private readonly Channel<Guid> _channel;

    public OptimizerQueueWriter(Channel<Guid> channel)
    {
        _channel = channel;
    }

    public bool Enqueue(Guid jobId) => _channel.Writer.TryWrite(jobId);
}
