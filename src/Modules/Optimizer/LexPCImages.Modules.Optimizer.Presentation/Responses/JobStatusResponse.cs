using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Application.UseCases.GetJobStatus;

namespace LexPCImages.Modules.Optimizer.Presentation.Responses;

/// <summary>Una de las salidas encoladas. El tamaño viaja para que el cliente pueda etiquetarla.</summary>
public sealed record EnqueuedJobResponse(Guid JobId, string SlotId, int Width, int Height, string Status);

/// <summary>
/// Respuesta del encolado. Siempre es una lista, también cuando el id pedido produce una sola
/// salida: así el cliente no tiene que distinguir entre un slot suelto y un paquete.
/// </summary>
public sealed record EnqueueJobResponse(IReadOnlyList<EnqueuedJobResponse> Jobs)
{
    public static EnqueueJobResponse From(EnqueueJobResult result) =>
        new([.. result.Jobs.Select(job => new EnqueuedJobResponse(
            job.JobId, job.SlotId.Value, job.Width, job.Height, job.Status.ToString()))]);
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
