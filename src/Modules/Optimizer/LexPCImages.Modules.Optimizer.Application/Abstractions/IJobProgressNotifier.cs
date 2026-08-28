using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IJobProgressNotifier
{
    Task OnStageStartedAsync(
        Guid jobId,
        ProcessingStage stage,
        int percent,
        CancellationToken cancellationToken);

    Task OnStageCompletedAsync(
        Guid jobId,
        ProcessingStage stage,
        int percent,
        CancellationToken cancellationToken);

    Task OnErrorAsync(Guid jobId, string message, CancellationToken cancellationToken);
}
