using LexPCImages.Modules.Optimizer.Application.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Pipelines;

/// <summary>
/// Datos de entrada de un pipeline. Se pasa el <see cref="JobId"/> —y no el agregado completo—
/// para que el pipeline no pueda mutar el estado del trabajo: de eso se encarga el notificador.
/// </summary>
public sealed record ImagePipelineContext(
    Guid JobId,
    DecodedImage Source,
    SlotDefinition Slot);

/// <summary>
/// Estrategia de transformación asociada a un <see cref="SlotMode"/>. Añadir un modo nuevo es
/// añadir una implementación y registrarla: no se toca el orquestador (principio abierto/cerrado).
/// </summary>
public interface IImageProcessingPipeline
{
    SlotMode Mode { get; }

    Task<DecodedImage> ExecuteAsync(ImagePipelineContext context, CancellationToken cancellationToken);
}
