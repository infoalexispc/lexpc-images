using System.Collections.Frozen;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Registries;

/// <summary>Catálogo estático de slots publicables. El conjunto se congela al construirse.</summary>
public sealed class SlotRegistry : ISlotRegistry
{
    private static readonly SlotDefinition[] KnownSlots =
    [
        SlotDefinition.PcHome,
        SlotDefinition.PcMainSection,
    ];

    private readonly FrozenDictionary<SlotId, SlotDefinition> _slots = KnownSlots.ToFrozenDictionary(slot => slot.Id);
    private readonly IReadOnlyList<SlotDefinition> _all = KnownSlots.AsReadOnly();

    public SlotDefinition? FindById(SlotId id) => _slots.GetValueOrDefault(id);

    public IReadOnlyList<SlotDefinition> All() => _all;
}
