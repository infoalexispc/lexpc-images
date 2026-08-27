using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Presentation.Responses;

public sealed record EnqueueJobResponse(Guid JobId, string Status);

public sealed record JobStatusResponse(
    Guid JobId,
    string Status,
    string? Stage,
    int Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);

public static class JobStatusResponseMapper
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
