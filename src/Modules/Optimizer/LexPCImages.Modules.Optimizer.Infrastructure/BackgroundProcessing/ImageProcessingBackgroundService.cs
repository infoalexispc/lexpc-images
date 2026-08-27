using System.Threading.Channels;
using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;

public sealed class ImageProcessingBackgroundService : BackgroundService
{
    private readonly Channel<Guid> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ImageProcessingBackgroundService> _logger;

    public ImageProcessingBackgroundService(
        Channel<Guid> queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ImageProcessingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public bool TryEnqueue(Guid jobId) => _queue.Writer.TryWrite(jobId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageProcessingBackgroundService started");
        await foreach (var jobId in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            _logger.LogDebug("Dequeued job {JobId}", jobId);
            try
            {
                await ProcessOneAsync(jobId, stoppingToken);
                _logger.LogDebug("Processed job {JobId}", jobId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background processing failed for job {JobId}", jobId);
            }
        }
        _logger.LogInformation("ImageProcessingBackgroundService stopped");
    }

    private async Task ProcessOneAsync(Guid jobId, CancellationToken stoppingToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var jobs = scope.ServiceProvider.GetRequiredService<IJobRepository>();
        var notifier = scope.ServiceProvider.GetRequiredService<IJobProgressNotifier>();
        var handler = scope.ServiceProvider.GetRequiredService<ProcessImageHandler>();

        var job = await jobs.GetAsync(jobId, stoppingToken);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} not found when dequeuing", jobId);
            return;
        }

        var result = await handler.HandleAsync(job, stoppingToken);
        if (result.IsFailure)
        {
            notifier.OnError(jobId, result.Error!.Message);
            return;
        }

        var refreshed = await jobs.GetAsync(jobId, stoppingToken);
        if (refreshed is null)
        {
            return;
        }
        refreshed.MarkDone(result.Value!, "image/webp");
    }
}
