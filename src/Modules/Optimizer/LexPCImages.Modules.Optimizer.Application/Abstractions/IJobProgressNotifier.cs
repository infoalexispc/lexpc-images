using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Abstractions;

public interface IJobProgressNotifier
{
    void OnStageStarted(Guid jobId, ProcessingStage stage, int percent);
    void OnStageCompleted(Guid jobId, ProcessingStage stage, int percent);
    void OnError(Guid jobId, string message);
}
