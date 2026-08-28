using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Application.Ports;

public interface ISlotRegistry
{
    SlotDefinition? FindById(SlotId id);
    IReadOnlyList<SlotDefinition> All();
}
