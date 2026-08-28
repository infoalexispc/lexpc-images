using FluentAssertions;
using LexPCImages.Modules.Optimizer.Application.UseCases.EnqueueJob;
using LexPCImages.Modules.Optimizer.Domain.ValueObjects;
using LexPCImages.Shared.Common;
using NetArchTest.Rules;

namespace LexPCImages.ArchitectureTests;

public sealed class LayerDependencyTests
{
    [Fact]
    public void Domain_should_reference_nothing_but_the_BCL()
    {
        // La capa mas interna no depende de nada del repositorio: ni de Shared, ni de paquetes.
        // Antes referenciaba Shared solo para OptimizerErrors, que era en realidad el catalogo
        // de respuestas de la API y por eso se movio a Application.
        static List<string> NonBclReferences(System.Reflection.Assembly assembly) => assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null && !IsBclAssembly(name))
            .Select(name => name!)
            .ToList();

        // El detector se autocomprueba: Application si referencia Shared, asi que si esta
        // comprobacion dejara de encontrarlo la regla de abajo pasaria siempre sin proteger nada.
        NonBclReferences(ArchitectureLayers.Application).Should().Contain(
            "LexPCImages.Shared",
            "si Application dejara de referenciar Shared, este test habria dejado de detectar nada");

        NonBclReferences(ArchitectureLayers.Domain).Should().BeEmpty(
            "el dominio debe ser autonomo: nada de proyectos propios ni de paquetes de terceros");
    }

    private static bool IsBclAssembly(string name) =>
        name.StartsWith("System", StringComparison.Ordinal)
        || name is "netstandard" or "mscorlib";

    [Fact]
    public void Domain_ShouldNotDependOnAnyOtherLayer()
    {
        var result = Types.InAssembly(typeof(SlotId).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Application",
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.Shared.Web",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain must not depend on any other layer. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldOnlyDependOnDomainAndShared()
    {
        var result = Types.InAssembly(ArchitectureLayers.Application)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.Shared.Web",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application must only depend on Domain and Shared. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Infrastructure_Module_ShouldNotDependOnPresentation()
    {
        var result = Types.InAssembly(ArchitectureLayers.Infrastructure)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Presentation",
                "LexPCImages.Shared.Web",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Optimizer.Infrastructure must not depend on Presentation. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Presentation_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InAssembly(ArchitectureLayers.Presentation)
            .ShouldNot()
            .HaveDependencyOnAny(
                "LexPCImages.Modules.Optimizer.Infrastructure",
                "LexPCImages.API")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Optimizer.Presentation must not depend on Infrastructure. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Shared_and_Domain_should_not_reference_AspNetCore()
    {
        var offenders = new[] { typeof(Result).Assembly, typeof(SlotId).Assembly }
            .SelectMany(assembly => assembly.GetReferencedAssemblies())
            .Where(reference => reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true)
            .Select(reference => reference.Name)
            .Distinct()
            .ToList();

        offenders.Should().BeEmpty(
            $"Shared and Domain must remain independent from ASP.NET Core. Fails: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Application_should_not_reference_Channels()
    {
        var offenders = typeof(EnqueueJobHandler).Assembly
            .GetReferencedAssemblies()
            .Where(reference => reference.Name == "System.Threading.Channels")
            .Select(reference => reference.Name)
            .ToList();

        offenders.Should().BeEmpty(
            $"Application must not depend on System.Threading.Channels. Fails: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Application_should_not_reference_ImageSharp_or_OnnxRuntime()
    {
        var offenders = ArchitectureLayers.Application
            .GetReferencedAssemblies()
            .Where(reference =>
                reference.Name?.StartsWith("SixLabors", StringComparison.Ordinal) == true
                || reference.Name?.StartsWith("Microsoft.ML.OnnxRuntime", StringComparison.Ordinal) == true)
            .Select(reference => reference.Name)
            .ToList();

        offenders.Should().BeEmpty(
            "las librerías de imagen e inferencia son detalles de Infrastructure y no deben filtrarse a Application. " +
            $"Fails: {string.Join(", ", offenders)}");
    }
}
