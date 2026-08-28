using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;

public sealed record GetJobStatusQuery(Guid JobId);

public sealed record JobStatusResult(
    Guid JobId,
    JobStatus Status,
    ProcessingStage? Stage,
    int Progress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);

public sealed class GetJobStatusHandler
{
    private readonly IJobRepository _jobs;

    public GetJobStatusHandler(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<Result<JobStatusResult>> HandleAsync(GetJobStatusQuery query, CancellationToken cancellationToken)
    {
        var job = await _jobs.GetAsync(query.JobId, cancellationToken);
        if (job is null)
        {
            return OptimizerErrors.JobNotFound(query.JobId.ToString());
        }

        return new JobStatusResult(
            job.Id,
            job.Status,
            job.CurrentStage,
            job.Progress,
            job.CreatedAt,
            job.CompletedAt,
            job.ErrorMessage);
    }
}
