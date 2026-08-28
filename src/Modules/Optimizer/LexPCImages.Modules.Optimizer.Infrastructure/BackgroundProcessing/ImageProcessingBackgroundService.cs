using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Application.UseCases.ProcessImage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;

/// <summary>
/// Consume la cola de trabajos y ejecuta el caso de uso de procesado en un ámbito propio.
/// Un fallo de un trabajo nunca detiene el bucle: se marca el trabajo y se sigue con el siguiente.
/// </summary>
public sealed class ImageProcessingBackgroundService : BackgroundService
{
    private const string GenericFailureMessage = "Image processing failed. Check the server logs for details.";

    private readonly IJobQueueReader _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<ImageProcessingBackgroundService> _logger;

    public ImageProcessingBackgroundService(
        IJobQueueReader queue,
        IServiceScopeFactory scopeFactory,
        TimeProvider time,
        ILogger<ImageProcessingBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _time = time;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ImageProcessingBackgroundService started");
        await foreach (var jobId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessOneAsync(jobId, stoppingToken);
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

        try
        {
            var result = await handler.HandleAsync(job, stoppingToken);
            if (result.IsFailure)
            {
                await notifier.OnErrorAsync(jobId, result.ErrorOrThrow().Message, stoppingToken);
                return;
            }

            await CompleteAsync(jobs, jobId, result.ValueOrThrow(), stoppingToken);
            _logger.LogDebug("Processed job {JobId}", jobId);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Background processing failed for job {JobId}", jobId);
            await MarkAsFailedAsync(jobs, jobId);
        }
    }

    private async Task CompleteAsync(
        IJobRepository jobs,
        Guid jobId,
        EncodedImage encoded,
        CancellationToken cancellationToken)
    {
        // Se relee el trabajo: el notificador de progreso lo ha actualizado durante el pipeline.
        var job = await jobs.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            _logger.LogWarning("Job {JobId} disappeared before it could be completed", jobId);
            return;
        }

        job.MarkDone(encoded.Content, encoded.ContentType, _time.GetUtcNow());
        await jobs.UpdateAsync(job, cancellationToken);
    }

    /// <summary>Cierra el trabajo con un mensaje genérico: el detalle real queda solo en los logs.</summary>
    private async Task MarkAsFailedAsync(IJobRepository jobs, Guid jobId)
    {
        try
        {
            var job = await jobs.GetAsync(jobId, CancellationToken.None);
            if (job is null || job.IsTerminal)
            {
                return;
            }

            job.MarkError(GenericFailureMessage, _time.GetUtcNow());
            await jobs.UpdateAsync(job, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark job {JobId} as error", jobId);
        }
    }
}
