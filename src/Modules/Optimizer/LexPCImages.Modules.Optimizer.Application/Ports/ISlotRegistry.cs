using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Ports;

public interface ISlotRegistry
{
    SlotDefinition? FindById(SlotId id);

    IReadOnlyList<SlotDefinition> All();

    /// <summary>
    /// Salidas que produce un id público. Un slot suelto resuelve a una lista de uno y un paquete
    /// a todas las suyas, así que quien encola no necesita saber cuál de los dos le han pedido.
    /// Devuelve una lista vacía si el id no existe.
    /// </summary>
    IReadOnlyList<SlotDefinition> Resolve(SlotId id);
}
