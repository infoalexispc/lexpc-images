using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;

/// <summary>
/// Traduce los avisos de progreso del pipeline a transiciones del agregado y los persiste.
/// </summary>
public sealed class JobProgressNotifier : IJobProgressNotifier
{
    private readonly IJobRepository _jobs;
    private readonly TimeProvider _time;
    private readonly ILogger<JobProgressNotifier> _logger;

    public JobProgressNotifier(IJobRepository jobs, TimeProvider time, ILogger<JobProgressNotifier> logger)
    {
        _jobs = jobs;
        _time = time;
        _logger = logger;
    }

    public Task OnStageStartedAsync(
        Guid jobId,
        ProcessingStage stage,
        int percent,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            jobId,
            job => job.MarkProcessing(stage, percent, _time.GetUtcNow()),
            nameof(OnStageStartedAsync),
            cancellationToken);

    public Task OnStageCompletedAsync(
        Guid jobId,
        ProcessingStage stage,
        int percent,
        CancellationToken cancellationToken) =>
        UpdateAsync(
            jobId,
            job => job.UpdateProgress(stage, percent),
            nameof(OnStageCompletedAsync),
            cancellationToken);

    public Task OnErrorAsync(Guid jobId, string message, CancellationToken cancellationToken) =>
        UpdateAsync(
            jobId,
            job => job.MarkError(message, _time.GetUtcNow()),
            nameof(OnErrorAsync),
            cancellationToken);

    private async Task UpdateAsync(
        Guid jobId,
        Action<ProcessJob> mutation,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _jobs.GetAsync(jobId, cancellationToken);
            if (job is null)
            {
                _logger.LogWarning("Progress update for unknown job {JobId} from {Source}", jobId, source);
                return;
            }

            mutation(job);
            await _jobs.UpdateAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply progress update {Source} for job {JobId}", source, jobId);
            throw;
        }
    }
}
