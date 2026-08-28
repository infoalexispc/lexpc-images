using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Application.Validation;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Application.Errors;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using Microsoft.Extensions.Logging;

namespace LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;

public sealed record RefinementOverrides(
    bool? SuppressShadow = null,
    bool? RemoveDesk = null,
    bool? ProtectLegs = null,
    double? CropMarginPct = null);

public sealed record EnqueueJobCommand(
    SlotId SlotId,
    byte[] ImageBytes,
    string ContentType,
    RefinementOverrides? Refinement = null);

public sealed record EnqueueJobResult(Guid JobId, JobStatus Status);

/// <summary>
/// Valida la petición, crea el trabajo y lo encola. Es el único punto donde se valida la entrada:
/// la capa web se limita a traducir el <see cref="Error"/> resultante a HTTP.
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

        var slot = _slots.FindById(command.SlotId);
        if (slot is null)
        {
            _logger.LogWarning("Slot {SlotId} not found in registry", command.SlotId);
            return OptimizerErrors.SlotNotFound;
        }

        if (ValidateImage(command.ImageBytes, command.ContentType) is { } validationError)
        {
            return validationError;
        }

        if (!slot.EffectiveRefinement.TryWith(
                command.Refinement?.SuppressShadow,
                command.Refinement?.RemoveDesk,
                command.Refinement?.ProtectLegs,
                command.Refinement?.CropMarginPct,
                out var refinement))
        {
            return OptimizerErrors.CropMarginOutOfRange;
        }

        var job = ProcessJob.Create(
            slot, command.ImageBytes, command.ContentType, _time.GetUtcNow(), refinement);
        await _jobs.AddAsync(job, cancellationToken);

        if (!_processingQueue.TryEnqueue(job.Id))
        {
            _logger.LogError("Failed to enqueue job {JobId} into the processing channel", job.Id);
            job.MarkError("The processing queue is full.", _time.GetUtcNow());
            await _jobs.UpdateAsync(job, cancellationToken);
            return OptimizerErrors.ProcessingQueueFull;
        }

        _logger.LogInformation(
            "Job {JobId} enqueued for slot {SlotId} ({Bytes} bytes, {ContentType}, shadow={Shadow}, desk={Desk}, legs={Legs}, margin={Margin})",
            job.Id, slot.Id, command.ImageBytes.Length, command.ContentType,
            refinement.SuppressShadow, refinement.RemoveDesk, refinement.ProtectLegs, refinement.CropMarginPct);

        return new EnqueueJobResult(job.Id, job.Status);
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
