using System.Collections.Frozen;
using LexPCImages.Modules.Optimizer.Application.Ports;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.Modules.Optimizer.Infrastructure.Registries;

/// <summary>Catálogo estático de slots publicables y de sus paquetes. Se congela al construirse.</summary>
public sealed class SlotRegistry : ISlotRegistry
{
    private static readonly SlotDefinition[] KnownSlots =
    [
        SlotDefinition.PcHomeSmall,
        SlotDefinition.PcHomeWide,
        SlotDefinition.PcMainSection,
        SlotDefinition.PcLastSection,
    ];

    private static readonly SlotBundle[] KnownBundles =
    [
        SlotBundle.PcHome,
    ];

    private readonly FrozenDictionary<SlotId, SlotDefinition> _slots = KnownSlots.ToFrozenDictionary(slot => slot.Id);
    private readonly IReadOnlyList<SlotDefinition> _all = KnownSlots.AsReadOnly();
    private readonly FrozenDictionary<SlotId, IReadOnlyList<SlotDefinition>> _bundles =
        KnownBundles.ToFrozenDictionary(bundle => bundle.Id, bundle => bundle.Outputs);

    public SlotDefinition? FindById(SlotId id) => _slots.GetValueOrDefault(id);

    public IReadOnlyList<SlotDefinition> All() => _all;

    public IReadOnlyList<SlotDefinition> Resolve(SlotId id)
    {
        if (_bundles.TryGetValue(id, out var outputs))
        {
            return outputs;
        }
        return _slots.TryGetValue(id, out var slot) ? [slot] : [];
    }
}
