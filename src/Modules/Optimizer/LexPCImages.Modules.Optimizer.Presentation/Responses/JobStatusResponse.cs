using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;

namespace LexPCImages.Modules.Optimizer.Presentation.Responses;

public sealed record EnqueueJobResponse(Guid JobId, string Status)
{
    public static EnqueueJobResponse From(EnqueueJobResult result) =>
        new(result.JobId, result.Status.ToString());
}

public sealed record JobStatusResponse(
    Guid JobId,
    string Status,
    string? Stage,
    int Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage)
{
    public static JobStatusResponse From(JobStatusResult result) => new(
        result.JobId,
        result.Status.ToString(),
        result.Stage?.ToString(),
        result.Progress,
        result.CreatedAt,
        result.CompletedAt,
        result.ErrorMessage);
}
