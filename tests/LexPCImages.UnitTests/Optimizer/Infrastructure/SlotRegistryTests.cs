using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Modules.Optimizer.Infrastructure.Registries;

namespace LexPCImages.UnitTests.Optimizer.Infrastructure;

public sealed class SlotRegistryTests
{
    private readonly SlotRegistry _registry = new();

    [Fact]
    public void Resolve_returns_a_single_output_for_a_plain_slot()
    {
        _registry.Resolve(SlotDefinition.PcMainSection.Id)
            .Should().ContainSingle().Which.Should().Be(SlotDefinition.PcMainSection);
    }

    [Fact]
    public void Resolve_expands_a_bundle_into_all_of_its_outputs()
    {
        _registry.Resolve(SlotBundle.PcHome.Id)
            .Should().Equal(SlotDefinition.PcHomeSmall, SlotDefinition.PcHomeWide);
    }

    [Fact]
    public void Resolve_returns_nothing_for_an_unknown_id()
    {
        _registry.Resolve(SlotId.Parse("no-existe")).Should().BeEmpty();
    }

    [Fact]
    public void All_lists_the_output_slots_but_not_the_bundles()
    {
        var ids = _registry.All().Select(slot => slot.Id.Value);

        ids.Should().Contain(SlotDefinition.PcHomeSmall.Id.Value);
        ids.Should().NotContain(SlotBundle.PcHome.Id.Value, "un paquete no es un destino publicable en si mismo");
    }

    [Fact]
    public void FindById_does_not_resolve_bundles()
    {
        _registry.FindById(SlotBundle.PcHome.Id).Should().BeNull();
        _registry.FindById(SlotDefinition.PcLastSection.Id).Should().Be(SlotDefinition.PcLastSection);
    }
}
