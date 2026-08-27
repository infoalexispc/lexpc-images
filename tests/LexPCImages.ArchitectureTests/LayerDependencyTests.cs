using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using NetArchTest.Rules;

namespace LexPCImages.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(typeof(SlotId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Application",
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.Infrastructure",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must not depend on any other layer. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldOnlyDependOnDomainAndShared()
    {
        var result = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IBackgroundRemovalService).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.Infrastructure",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must only depend on Domain and Shared. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_Module_ShouldNotDependOnPresentation()
    {
        var result = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Infrastructure.AssemblyMarker).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Optimizer.Infrastructure must not depend on Presentation. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Presentation_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Presentation.OptimizerModule).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Optimizer.Presentation must not depend on Infrastructure. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
