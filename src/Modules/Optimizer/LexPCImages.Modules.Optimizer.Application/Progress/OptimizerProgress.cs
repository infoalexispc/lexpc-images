using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Progress;

/// <summary>
/// Reparto declarativo del progreso entre etapas. Es la única fuente de verdad de los porcentajes:
/// antes estaban repartidos como literales por todo el orquestador y era imposible mantenerlos coherentes.
/// </summary>
public static class OptimizerProgress
{
    public static readonly StageProgress Decoding = new(ProcessingStage.Decoding, 5, 15);

    /// <summary>Tramo de la transformación. Los tres pipelines lo ocupan entero con una sola etapa.</summary>
    public static readonly StageProgress Resizing = new(ProcessingStage.Resizing, 15, 90);

    public static readonly StageProgress Encoding = new(ProcessingStage.Encoding, 92, 100);
}
