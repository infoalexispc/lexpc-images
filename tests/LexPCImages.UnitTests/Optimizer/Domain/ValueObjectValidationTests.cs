using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.UnitTests.Optimizer.Domain;

public sealed class ValueObjectValidationTests
{
    [Fact]
    public void SlotId_rejects_empty_values()
    {
        var act = () => SlotId.Parse(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SlotId_trims_the_parsed_value()
    {
        SlotId.Parse("  slot  ").Value.Should().Be("slot");
    }







    [Fact]
    public void SlotDefinition_rejects_non_positive_dimensions()
    {
        var act = () => new SlotDefinition(SlotId.Parse("slot"), 0, 100);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SlotDefinition_rejects_an_unknown_mode()
    {
        var act = () => new SlotDefinition(SlotId.Parse("slot"), 10, 10, mode: (SlotMode)99);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }


    [Fact]
    public void SlotDefinition_falls_back_to_the_default_cover_fit()
    {
        var slot = new SlotDefinition(SlotId.Parse("slot"), 10, 10);

        slot.CoverFit.Should().BeNull();
        slot.EffectiveCoverFit.Should().Be(CoverFitOptions.Defaults);
    }

    [Fact]
    public void SlotBundle_rejects_an_empty_or_repeated_set_of_outputs()
    {
        var empty = () => new SlotBundle(SlotId.Parse("bundle"), []);
        var repeated = () => new SlotBundle(
            SlotId.Parse("bundle"), [SlotDefinition.PcHomeSmall, SlotDefinition.PcHomeSmall]);

        empty.Should().Throw<ArgumentException>();
        repeated.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void PcHome_bundle_produces_320x315_and_992x715_without_a_background()
    {
        var outputs = SlotBundle.PcHome.Outputs;

        SlotBundle.PcHome.Id.Value.Should().Be("optimizar-imagen-pc-home");
        outputs.Select(slot => (slot.Width, slot.Height)).Should().Equal((320, 315), (992, 715));
        outputs.Should().OnlyContain(slot => slot.Mode == SlotMode.FitTransparent);
    }

    [Fact]
    public void PcLastSection_targets_619x720_in_cover_or_pad_mode()
    {
        var slot = SlotDefinition.PcLastSection;

        slot.Id.Value.Should().Be("optimizar-imagen-pc-ultima-seccion");
        slot.Width.Should().Be(619);
        slot.Height.Should().Be(720);
        slot.Mode.Should().Be(SlotMode.CoverOrPad);
        slot.EffectiveCoverFit.Should().Be(CoverFitOptions.Defaults);
    }
}
