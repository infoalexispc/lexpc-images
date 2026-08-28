using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.GetJobDownload;

public sealed record GetJobDownloadQuery(Guid JobId);

public sealed record JobDownloadResult(
    byte[] Content,
    string ContentType,
    string FileName,
    JobStatus Status,
    int Progress);

public sealed class GetJobDownloadHandler
{
    private const string DefaultExtension = "bin";

    private static readonly Dictionary<string, string> ExtensionsByContentType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/webp"] = "webp",
            ["image/png"] = "png",
            ["image/jpeg"] = "jpg",
        };

    private readonly IJobRepository _jobs;

    public GetJobDownloadHandler(IJobRepository jobs)
    {
        _jobs = jobs;
    }

    public async Task<Result<JobDownloadResult>> HandleAsync(
        GetJobDownloadQuery query,
        CancellationToken cancellationToken)
    {
        var job = await _jobs.GetAsync(query.JobId, cancellationToken);
        if (job is null)
        {
            return OptimizerErrors.JobNotFound(query.JobId.ToString());
        }

        if (job.Status != JobStatus.Done
            || job.OutputImage is not { } content
            || job.OutputContentType is not { } contentType)
        {
            return OptimizerErrors.JobNotReady(job.Status.ToString());
        }

        return new JobDownloadResult(
            content,
            contentType,
            BuildFileName(job, contentType),
            job.Status,
            job.Progress);
    }

    /// <summary>Nombre derivado del slot y del formato real; antes se emitía siempre "pc-home-….webp".</summary>
    private static string BuildFileName(ProcessJob job, string contentType)
    {
        var extension = ExtensionsByContentType.GetValueOrDefault(contentType, DefaultExtension);
        return $"{job.Slot.Id.Value}-{job.Id:N}.{extension}";
    }
}
