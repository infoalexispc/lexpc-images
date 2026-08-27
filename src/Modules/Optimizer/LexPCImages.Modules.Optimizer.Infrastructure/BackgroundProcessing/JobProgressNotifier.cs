using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Infrastructure.BackgroundProcessing;

public sealed class JobProgressNotifier : IJobProgressNotifier
{
    private readonly IJobRepository _jobs;
    private readonly ILogger<JobProgressNotifier> _logger;

    public JobProgressNotifier(IJobRepository jobs, ILogger<JobProgressNotifier> logger)
    {
        _jobs = jobs;
        _logger = logger;
    }

    public void OnStageStarted(Guid jobId, ProcessingStage stage, int percent)
    {
        UpdateAsync(jobId, j => j.MarkProcessing(stage, percent), nameof(OnStageStarted));
    }

    public void OnStageCompleted(Guid jobId, ProcessingStage stage, int percent)
    {
        UpdateAsync(jobId, j => j.UpdateProgress(stage, percent), nameof(OnStageCompleted));
    }

    public void OnError(Guid jobId, string message)
    {
        UpdateAsync(jobId, j => j.MarkError(message), nameof(OnError));
    }

    private void UpdateAsync(Guid jobId, Action<Domain.Entities.ProcessJob> mutation, string source)
    {
        try
        {
            var job = _jobs.GetAsync(jobId, CancellationToken.None).GetAwaiter().GetResult();
            if (job is null)
            {
                _logger.LogWarning("Progress update for unknown job {JobId} from {Source}", jobId, source);
                return;
            }
            mutation(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply progress update {Source} for job {JobId}", source, jobId);
        }
    }
}
