using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Progress;

/// <summary>
/// Reparto declarativo del progreso entre etapas. Es la única fuente de verdad de los porcentajes:
/// antes estaban repartidos como literales por todo el orquestador y era imposible mantenerlos coherentes.
/// </summary>
public static class OptimizerProgress
{
    // Etapas comunes a todos los pipelines.
    public static readonly StageProgress Decoding = new(ProcessingStage.Decoding, 5, 15);
    public static readonly StageProgress Encoding = new(ProcessingStage.Encoding, 92, 100);

    // Pipeline de eliminación de fondo: reparte el tramo [15, 90].
    public static readonly StageProgress Inferring = new(ProcessingStage.Inferring, 15, 50);
    public static readonly StageProgress LegProtecting = new(ProcessingStage.LegProtecting, 50, 58);
    public static readonly StageProgress DeskRemoving = new(ProcessingStage.DeskRemoving, 58, 66);
    public static readonly StageProgress ShadowSuppressing = new(ProcessingStage.ShadowSuppressing, 66, 74);
    public static readonly StageProgress Cropping = new(ProcessingStage.Cropping, 74, 82);
    public static readonly StageProgress Resizing = new(ProcessingStage.Resizing, 82, 90);

    // Pipeline de redimensionado con relleno: ocupa el tramo [15, 90] con una sola etapa.
    public static readonly StageProgress ResizingAndPadding = new(ProcessingStage.Resizing, 15, 90);

    // Pipeline de recorte o relleno: mismo tramo y misma etapa, la decision no la ve el cliente.
    public static readonly StageProgress CoverFitting = new(ProcessingStage.Resizing, 15, 90);
}
