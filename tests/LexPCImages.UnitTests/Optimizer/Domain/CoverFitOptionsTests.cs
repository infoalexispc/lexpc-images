using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;

namespace LexPCImages.UnitTests.Optimizer.Domain;

public sealed class CoverFitOptionsTests
{
    private const int TargetWidth = 619;
    private const int TargetHeight = 720;

    [Theory]
    [InlineData(619, 720)]   // ya tiene la proporcion del slot: no se pierde nada
    [InlineData(1000, 1000)] // cuadrada
    [InlineData(800, 1000)]  // 4:5
    [InlineData(900, 1200)]  // 3:4
    public void ShouldCrop_is_true_when_the_aspect_ratio_is_close_to_the_slot(int width, int height)
    {
        CoverFitOptions.Defaults
            .ShouldCrop(width, height, TargetWidth, TargetHeight)
            .Should().BeTrue();
    }

    [Theory]
    [InlineData(1000, 1500)] // 2:3, ya se comeria mas del 22%
    [InlineData(1600, 1200)] // 4:3
    [InlineData(1500, 1000)] // 3:2
    [InlineData(1920, 1080)] // 16:9
    [InlineData(1080, 1920)] // 9:16
    public void ShouldCrop_is_false_when_cropping_would_eat_too_much_of_the_image(int width, int height)
    {
        CoverFitOptions.Defaults
            .ShouldCrop(width, height, TargetWidth, TargetHeight)
            .Should().BeFalse();
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    public void ShouldCrop_falls_back_to_padding_for_invalid_dimensions(int width, int height)
    {
        CoverFitOptions.Defaults
            .ShouldCrop(width, height, TargetWidth, TargetHeight)
            .Should().BeFalse();
    }

    [Fact]
    public void CoverageOf_is_one_when_both_aspect_ratios_match()
    {
        CoverFitOptions.CoverageOf(1238, 1440, TargetWidth, TargetHeight)
            .Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void CoverageOf_is_symmetric_between_source_and_target()
    {
        var forward = CoverFitOptions.CoverageOf(1920, 1080, TargetWidth, TargetHeight);
        var backward = CoverFitOptions.CoverageOf(TargetWidth, TargetHeight, 1920, 1080);

        forward.Should().BeApproximately(backward, 0.0001);
    }

    [Fact]
    public void A_permissive_threshold_crops_what_the_default_would_pad()
    {
        var permissive = new CoverFitOptions(minCoverage: 0.4);

        permissive.ShouldCrop(1920, 1080, TargetWidth, TargetHeight).Should().BeTrue();
        CoverFitOptions.Defaults.ShouldCrop(1920, 1080, TargetWidth, TargetHeight).Should().BeFalse();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Rejects_a_threshold_outside_the_domain_range(double minCoverage)
    {
        var act = () => new CoverFitOptions(minCoverage);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void TryCreate_reports_an_invalid_threshold_without_throwing(double minCoverage)
    {
        var created = CoverFitOptions.TryCreate(minCoverage, out var options);

        created.Should().BeFalse();
        options.Should().BeNull();
    }

    [Fact]
    public void TryCreate_builds_a_valid_instance()
    {
        var created = CoverFitOptions.TryCreate(0.6, out var options);

        created.Should().BeTrue();
        options!.MinCoverage.Should().Be(0.6);
    }

    [Fact]
    public void Defaults_favour_padding_over_cropping()
    {
        CoverFitOptions.Defaults.MinCoverage.Should().Be(CoverFitOptions.DefaultMinCoverage);
        CoverFitOptions.DefaultMinCoverage.Should().BeGreaterThan(0.5);
    }
}
