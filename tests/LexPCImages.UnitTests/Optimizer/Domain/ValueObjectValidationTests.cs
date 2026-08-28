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

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.51)]
    public void RefinementOptions_rejects_margins_outside_the_domain_range(double margin)
    {
        var act = () => new RefinementOptions(cropMarginPct: margin);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RefinementOptions_rejects_non_finite_margins()
    {
        var act = () => new RefinementOptions(cropMarginPct: double.NaN);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(0.51)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RefinementOptions_TryCreate_reports_invalid_margins_without_throwing(double margin)
    {
        var created = RefinementOptions.TryCreate(true, true, true, margin, out var options);

        created.Should().BeFalse();
        options.Should().BeNull();
    }

    [Fact]
    public void RefinementOptions_TryCreate_builds_a_valid_instance()
    {
        var created = RefinementOptions.TryCreate(false, true, false, 0.25, out var options);

        created.Should().BeTrue();
        options!.SuppressShadow.Should().BeFalse();
        options.RemoveDesk.Should().BeTrue();
        options.ProtectLegs.Should().BeFalse();
        options.CropMarginPct.Should().Be(0.25);
    }

    [Fact]
    public void RefinementOptions_TryWith_keeps_the_values_that_are_not_overridden()
    {
        var applied = RefinementOptions.Defaults.TryWith(
            suppressShadow: false, removeDesk: null, protectLegs: null, cropMarginPct: null, out var options);

        applied.Should().BeTrue();
        options!.SuppressShadow.Should().BeFalse();
        options.RemoveDesk.Should().Be(RefinementOptions.Defaults.RemoveDesk);
        options.CropMarginPct.Should().Be(RefinementOptions.Defaults.CropMarginPct);
    }

    [Fact]
    public void RefinementOptions_TryWith_rejects_an_out_of_range_override()
    {
        var applied = RefinementOptions.Defaults.TryWith(null, null, null, 0.9, out var options);

        applied.Should().BeFalse();
        options.Should().BeNull();
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
    public void SlotDefinition_falls_back_to_the_default_refinement()
    {
        var slot = new SlotDefinition(SlotId.Parse("slot"), 10, 10);

        slot.EffectiveRefinement.Should().Be(RefinementOptions.Defaults);
    }
}
