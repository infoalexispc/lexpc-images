using System.Threading.Channels;
using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Modules.Optimizer.Domain.Errors;
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

public sealed class EnqueueJobHandler
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
    };

    private readonly IJobRepository _jobs;
    private readonly ISlotRegistry _slots;
    private readonly Channel<Guid> _processingQueue;
    private readonly ILogger<EnqueueJobHandler> _logger;
    private readonly TimeProvider _time;

    public EnqueueJobHandler(
        IJobRepository jobs,
        ISlotRegistry slots,
        Channel<Guid> processingQueue,
        ILogger<EnqueueJobHandler> logger,
        TimeProvider time)
    {
        _jobs = jobs;
        _slots = slots;
        _processingQueue = processingQueue;
        _logger = logger;
        _time = time;
    }

    public async Task<Result<EnqueueJobResult>> HandleAsync(EnqueueJobCommand command, CancellationToken cancellationToken)
    {
        var slot = _slots.FindById(command.SlotId);
        if (slot is null)
        {
            _logger.LogWarning("Slot {SlotId} not found in registry", command.SlotId);
            return OptimizerErrors.SlotNotFound;
        }

        var validation = ValidateImage(command.ImageBytes, command.ContentType);
        if (validation is not null)
        {
            return validation;
        }

        var refinement = ApplyOverrides(slot.EffectiveRefinement, command.Refinement);
        var job = ProcessJob.Create(
            slot, command.ImageBytes, command.ContentType, _time.GetUtcNow(), refinement);
        await _jobs.AddAsync(job, cancellationToken);

        if (!_processingQueue.Writer.TryWrite(job.Id))
        {
            _logger.LogError("Failed to enqueue job {JobId} into the processing channel", job.Id);
            job.MarkError("Failed to enqueue the job for processing.");
            return OptimizerErrors.InternalProcessingEnqueueFailed;
        }

        _logger.LogInformation(
            "Job {JobId} enqueued for slot {SlotId} ({Bytes} bytes, {ContentType}, shadow={Shadow}, desk={Desk}, legs={Legs}, margin={Margin})",
            job.Id, slot.Id, command.ImageBytes.Length, command.ContentType,
            refinement.SuppressShadow, refinement.RemoveDesk, refinement.ProtectLegs, refinement.CropMarginPct);

        return new EnqueueJobResult(job.Id, job.Status);
    }

    private static RefinementOptions ApplyOverrides(
        RefinementOptions defaults,
        RefinementOverrides? overrides)
    {
        if (overrides is null)
        {
            return defaults;
        }
        try
        {
            return defaults.With(
                suppressShadow: overrides.SuppressShadow,
                removeDesk: overrides.RemoveDesk,
                protectLegs: overrides.ProtectLegs,
                cropMarginPct: overrides.CropMarginPct);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new ArgumentOutOfRangeException(
                "refinement", ex.ParamName, ex.Message);
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
        if (!AllowedContentTypes.Contains(contentType))
        {
            return OptimizerErrors.ImageFormatNotSupported;
        }
        return null;
    }
}
