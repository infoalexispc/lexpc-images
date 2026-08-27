using LexPCImages.Modules.Optimizer.Domain.Abstractions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Registries;

public sealed class SlotRegistry : ISlotRegistry
{
    private readonly Dictionary<SlotId, SlotDefinition> _slots;

    public SlotRegistry()
    {
        _slots = new[]
        {
            SlotDefinition.PcHome,
            SlotDefinition.PcMainSection,
        }.ToDictionary(s => s.Id);
    }

    public SlotDefinition? FindById(SlotId id) =>
        _slots.TryGetValue(id, out var slot) ? slot : null;

    public IReadOnlyList<SlotDefinition> All() => _slots.Values.ToList();
}
