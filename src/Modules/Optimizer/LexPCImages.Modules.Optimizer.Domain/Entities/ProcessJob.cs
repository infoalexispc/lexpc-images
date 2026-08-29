using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Domain.Entities;

/// <summary>
/// Agregado raíz del módulo. Encapsula la máquina de estados de un trabajo de optimización.
/// El reloj se pasa siempre desde fuera (<c>DateTimeOffset now</c>): el dominio no consulta
/// <see cref="DateTimeOffset.UtcNow"/> para no depender del reloj del sistema.
/// </summary>
public sealed class ProcessJob
{
    public const int MaxInputBytes = 15 * 1024 * 1024;
    public const int MinWidth = 200;
    public const int MinHeight = 200;
    public const int MaxWidth = 8000;
    public const int MaxHeight = 8000;
    public const int MinProgress = 0;
    public const int MaxProgress = 100;

    public Guid Id { get; }
    public SlotDefinition Slot { get; }
    public byte[] InputImage { get; }
    public string InputContentType { get; }
    public JobStatus Status { get; private set; }
    public ProcessingStage? CurrentStage { get; private set; }
    public int Progress { get; private set; }
    public string? OutputContentType { get; private set; }
    public byte[]? OutputImage { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>Un trabajo es terminal cuando ya no puede volver a cambiar de estado.</summary>
    public bool IsTerminal => Status is JobStatus.Done or JobStatus.Error;

    private ProcessJob(
        Guid id,
        SlotDefinition slot,
        byte[] inputImage,
        string inputContentType,
        DateTimeOffset createdAt)
    {
        Id = id;
        Slot = slot;
        InputImage = inputImage;
        InputContentType = inputContentType;
        Status = JobStatus.Queued;
        Progress = MinProgress;
        CreatedAt = createdAt;
    }

    public static ProcessJob Create(
        SlotDefinition slot,
        byte[] inputImage,
        string inputContentType,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(inputImage);
        if (inputImage.Length == 0)
        {
            throw new ArgumentException("Input image cannot be empty.", nameof(inputImage));
        }
        if (inputImage.Length > MaxInputBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(inputImage), "Input image is too large.");
        }
        if (string.IsNullOrWhiteSpace(inputContentType))
        {
            throw new ArgumentException("Input content type cannot be empty.", nameof(inputContentType));
        }

        return new(Guid.NewGuid(), slot, inputImage, inputContentType, now);
    }

    public void MarkProcessing(ProcessingStage stage, int percent, DateTimeOffset now)
    {
        EnsureTransition(JobStatus.Queued, JobStatus.Processing);
        EnsureValidProgress(percent);

        Status = JobStatus.Processing;
        CurrentStage = stage;
        Progress = percent;
        StartedAt ??= now;
    }

    public void UpdateProgress(ProcessingStage stage, int percent)
    {
        if (Status != JobStatus.Processing)
        {
            throw new InvalidOperationException($"Cannot update progress of job in status {Status}.");
        }
        EnsureValidProgress(percent);

        CurrentStage = stage;
        Progress = percent;
    }

    /// <summary>
    /// Cierra el trabajo con éxito. Solo es válido desde <see cref="JobStatus.Processing"/>;
    /// repetir la llamada sobre un trabajo ya terminado es idempotente y no altera el resultado.
    /// </summary>
    public void MarkDone(byte[] outputImage, string outputContentType, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outputImage);
        if (outputImage.Length == 0)
        {
            throw new ArgumentException("Output image cannot be empty.", nameof(outputImage));
        }
        if (string.IsNullOrWhiteSpace(outputContentType))
        {
            throw new ArgumentException("Output content type cannot be empty.", nameof(outputContentType));
        }

        if (Status == JobStatus.Done)
        {
            return;
        }
        EnsureTransition(JobStatus.Processing);

        Status = JobStatus.Done;
        CurrentStage = null;
        Progress = MaxProgress;
        OutputImage = outputImage;
        OutputContentType = outputContentType;
        CompletedAt = now;
    }

    /// <summary>
    /// Cierra el trabajo con error. Es idempotente sobre un trabajo ya fallido y se rechaza
    /// sobre uno ya completado con éxito.
    /// </summary>
    public void MarkError(string message, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Error message cannot be empty.", nameof(message));
        }
        if (Status == JobStatus.Done)
        {
            throw new InvalidOperationException("Cannot mark an already-done job as error.");
        }

        Status = JobStatus.Error;
        CurrentStage = null;
        ErrorMessage = message;
        CompletedAt = now;
    }

    private static void EnsureValidProgress(int percent)
    {
        if (percent is < MinProgress or > MaxProgress)
        {
            throw new ArgumentOutOfRangeException(
                nameof(percent), percent, $"Progress must be {MinProgress}-{MaxProgress}.");
        }
    }

    private void EnsureTransition(params JobStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new InvalidOperationException(
                $"Invalid transition from {Status}. Allowed: {string.Join(", ", allowed)}.");
        }
    }
}
