using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Progress;

/// <summary>Tramo de progreso que ocupa una etapa del pipeline: <c>[Start, End]</c> en porcentaje.</summary>
public readonly record struct StageProgress
{
    public ProcessingStage Stage { get; }
    public int Start { get; }
    public int End { get; }

    public StageProgress(ProcessingStage stage, int start, int end)
    {
        if (start is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(start), start, "Start must be 0-100.");
        }
        if (end is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "End must be 0-100.");
        }
        if (end < start)
        {
            throw new ArgumentOutOfRangeException(nameof(end), end, "End must not be lower than start.");
        }

        Stage = stage;
        Start = start;
        End = end;
    }
}
