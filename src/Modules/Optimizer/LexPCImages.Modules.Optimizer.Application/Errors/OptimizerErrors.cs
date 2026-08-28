using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Shared.Common.Errors;

namespace LexPCImages.Modules.Optimizer.Application.Errors;

/// <summary>
/// Catálogo de errores que la aplicación devuelve al exterior. Vive en Application y no en
/// Domain a propósito: un <c>code</c> como <c>optimizer.image_too_large</c> y su mensaje son
/// parte del contrato de la API, no vocabulario del negocio. Las reglas de negocio de verdad
/// (transiciones de <see cref="ProcessJob"/>, rango de <c>CropMarginPct</c>) viven en el dominio
/// y se defienden con excepciones.
/// </summary>
public static class OptimizerErrors
{
    public static readonly Error SlotNotFound = Error.NotFound(
        "optimizer.slot_not_found",
        "The requested slot is not registered.");

    public static readonly Error ImageEmpty = Error.Validation(
        "optimizer.image_empty",
        "The uploaded image is empty.");

    public static readonly Error ImageTooLarge = Error.Validation(
        "optimizer.image_too_large",
        $"The uploaded image exceeds the maximum size of {ProcessJob.MaxInputBytes / 1024 / 1024} MB.");

    public static readonly Error ImageTooSmall = Error.Validation(
        "optimizer.image_too_small",
        $"The uploaded image must be at least {ProcessJob.MinWidth}x{ProcessJob.MinHeight} pixels.");

    public static readonly Error ImageDimensionsTooLarge = Error.Validation(
        "optimizer.image_dimensions_too_large",
        $"The uploaded image must be at most {ProcessJob.MaxWidth}x{ProcessJob.MaxHeight} pixels.");

    public static readonly Error ImageFormatNotSupported = Error.Validation(
        "optimizer.image_format_not_supported",
        "The uploaded image format is not supported (allowed: JPEG, PNG, WebP).");

    public static readonly Error ImageContentDoesNotMatchDeclaredFormat = Error.Validation(
        "optimizer.image_content_mismatch",
        "The uploaded file is not a valid JPEG, PNG or WebP image.");

    public static Error JobNotFound(string jobId) => Error.NotFound(
        "optimizer.job_not_found",
        $"No job with id '{jobId}' was found.");

    public static readonly Error InternalProcessingEnqueueFailed = Error.Internal(
        "optimizer.enqueue_failed",
        "The job was created but could not be enqueued for processing.");

    public static readonly Error ProcessingQueueFull = Error.Unavailable(
        "optimizer.processing_queue_full",
        "The image processing queue is full. Try again later.");

    public static readonly Error SlotIdRequired = Error.Validation(
        "optimizer.slot_id_required",
        "slotId is required.");

    public static readonly Error FileRequired = Error.Validation(
        "optimizer.file_required",
        "file is required and cannot be empty.");

    public static readonly Error CropMarginOutOfRange = Error.Validation(
        "optimizer.crop_margin_out_of_range",
        "cropMarginPct must be between 0 and 0.5.");

    public static Error PipelineNotAvailable(string mode) => Error.Internal(
        "optimizer.pipeline_not_available",
        $"No processing pipeline is registered for slot mode '{mode}'.");

    public static Error JobNotReady(string status) => Error.Conflict(
        "optimizer.job_not_ready",
        $"Job is in status '{status}'. Download is only available when status is 'Done'.");
}
