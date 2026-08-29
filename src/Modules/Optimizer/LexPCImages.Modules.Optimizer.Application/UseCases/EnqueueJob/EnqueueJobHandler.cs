using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Application.Validation;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Application.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;

public sealed record EnqueueJobCommand(
    SlotId SlotId,
    byte[] ImageBytes,
    string ContentType);

/// <summary>Un trabajo encolado, con el tamaño que va a producir para que el cliente pueda etiquetarlo.</summary>
public sealed record EnqueuedJob(Guid JobId, SlotId SlotId, int Width, int Height, JobStatus Status);

public sealed record EnqueueJobResult(IReadOnlyList<EnqueuedJob> Jobs);

/// <summary>
/// Valida la petición, crea los trabajos y los encola. Es el único punto donde se valida la
/// entrada: la capa web se limita a traducir el <see cref="Error"/> resultante a HTTP.
/// Un id puede resolver a varias salidas (paquete), y entonces la misma imagen genera un trabajo
/// por salida: el invariante "un trabajo produce una imagen" se mantiene intacto.
/// </summary>
public sealed class EnqueueJobHandler
{
    private readonly IJobRepository _jobs;
    private readonly ISlotRegistry _slots;
    private readonly IJobQueueWriter _processingQueue;
    private readonly ILogger<EnqueueJobHandler> _logger;
    private readonly TimeProvider _time;

    public EnqueueJobHandler(
        IJobRepository jobs,
        ISlotRegistry slots,
        IJobQueueWriter processingQueue,
        ILogger<EnqueueJobHandler> logger,
        TimeProvider time)
    {
        _jobs = jobs;
        _slots = slots;
        _processingQueue = processingQueue;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<EnqueueJobResult>> HandleAsync(
        EnqueueJobCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var targets = _slots.Resolve(command.SlotId);
        if (targets.Count == 0)
        {
            _logger.LogWarning("Slot {SlotId} not found in registry", command.SlotId);
            return OptimizerErrors.SlotNotFound;
        }

        // La imagen se valida una sola vez aunque se publique en varias salidas.
        if (ValidateImage(command.ImageBytes, command.ContentType) is { } validationError)
        {
            return validationError;
        }

        var enqueued = new List<EnqueuedJob>(targets.Count);
        var created = new List<ProcessJob>(targets.Count);

        foreach (var slot in targets)
        {
            var job = ProcessJob.Create(slot, command.ImageBytes, command.ContentType, _time.GetUtcNow());
            await _jobs.AddAsync(job, cancellationToken);
            created.Add(job);

            if (!_processingQueue.TryEnqueue(job.Id))
            {
                _logger.LogError("Failed to enqueue job {JobId} into the processing channel", job.Id);
                await FailAllAsync(created, cancellationToken);
                return OptimizerErrors.ProcessingQueueFull;
            }

            enqueued.Add(new EnqueuedJob(job.Id, slot.Id, slot.Width, slot.Height, job.Status));
        }

        _logger.LogInformation(
            "Enqueued {Count} job(s) for {SlotId} ({Bytes} bytes, {ContentType}): {JobIds}",
            enqueued.Count, command.SlotId, command.ImageBytes.Length, command.ContentType,
            string.Join(", ", enqueued.Select(job => job.JobId)));

        return new EnqueueJobResult(enqueued);
    }

    /// <summary>
    /// Si la cola se llena a mitad del paquete, los trabajos ya creados no pueden quedarse en
    /// <see cref="JobStatus.Queued"/> para siempre: se cierran en error para que el repositorio
    /// los descarte al cumplirse la retención.
    /// </summary>
    private async Task FailAllAsync(IEnumerable<ProcessJob> jobs, CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        foreach (var job in jobs.Where(job => !job.IsTerminal))
        {
            job.MarkError("The processing queue is full.", now);
            await _jobs.UpdateAsync(job, cancellationToken);
        }
    }

    private static Error? ValidateImage(byte[] imageBytes, string contentType)
    {
        if (imageBytes is null || imageBytes.Length == 0)
        {
            return OptimizerErrors.ImageEmpty;
        }
        if (imageBytes.Length > ProcessJob.MaxInputBytes)
        {
            return OptimizerErrors.ImageTooLarge;
        }
        if (!ImageContentTypes.IsAllowed(contentType))
        {
            return OptimizerErrors.ImageFormatNotSupported;
        }
        // El Content-Type lo declara el cliente: se contrasta con la firma real del fichero.
        if (!ImageContentTypes.HasSupportedSignature(imageBytes))
        {
            return OptimizerErrors.ImageContentDoesNotMatchDeclaredFormat;
        }
        return null;
    }
}
