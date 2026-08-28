using LexPCImages.Modules.Optimizer.Application.Abstractions;

namespace LexPCImages.Modules.Optimizer.Application.Progress;

/// <summary>
/// Azúcar sobre <see cref="IJobProgressNotifier"/> para notificar un tramo completo sin repetir
/// la etapa ni el porcentaje en cada llamada.
/// </summary>
public static class JobProgressNotifierExtensions
{
    public static Task BeginAsync(
        this IJobProgressNotifier notifier,
        Guid jobId,
        StageProgress stage,
        CancellationToken cancellationToken) =>
        notifier.OnStageStartedAsync(jobId, stage.Stage, stage.Start, cancellationToken);

    public static Task CompleteAsync(
        this IJobProgressNotifier notifier,
        Guid jobId,
        StageProgress stage,
        CancellationToken cancellationToken) =>
        notifier.OnStageCompletedAsync(jobId, stage.Stage, stage.End, cancellationToken);
}
