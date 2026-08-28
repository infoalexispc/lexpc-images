using FluentAssertions;
using LexPCImages.Modules.Optimizer.Domain.Entities;
using LexPCImages.Shared.Common.Errors;
using NetArchTest.Rules;

namespace LexPCImages.ArchitectureTests;

public sealed class DddConventionTests
{
    [Fact]
    public void Domain_classes_named_Entity_should_reside_in_Domain_namespace()
    {
        var result = Types.InAssembly(ArchitectureLayers.Domain)
            .That()
            .HaveNameEndingWith("Entity")
            .Should()
            .ResideInNamespace("LexPCImages.Modules.Optimizer.Domain")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Entity types must live in LexPCImages.Modules.Optimizer.Domain. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_aggregate_state_should_only_change_through_methods()
    {
        // Las propiedades de estado (Status, Progress, CurrentStage, Output*, Error*) se modifican
        // con MarkProcessing / UpdateProgress / MarkDone / MarkError, no con setters públicos.
        var statePropertyNames = new[]
        {
            "Status", "CurrentStage", "Progress", "OutputImage", "OutputContentType",
            "ErrorMessage", "StartedAt", "CompletedAt",
        };

        var publicSetters = statePropertyNames
            .Select(name => typeof(ProcessJob).GetProperty(name))
            .Where(property => property?.GetSetMethod() is { IsPublic: true })
            .Select(property => property!.Name)
            .ToList();

        publicSetters.Should().BeEmpty(
            "ProcessJob must expose state changes only through methods. " +
            $"Offenders: {string.Join(", ", publicSetters)}");
    }

    [Fact]
    public void Domain_should_not_read_the_ambient_clock()
    {
        // El agregado recibe la marca de tiempo desde fuera; si vuelve a llamar a DateTimeOffset.UtcNow
        // deja de ser determinista y el TimeProvider inyectado pierde sentido.
        var offenders = ArchitectureLayers.Domain
            .GetTypes()
            .Where(type => ClockAccessDetector.ReadsAmbientClock(type))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "el dominio debe recibir la hora como parámetro en lugar de leer el reloj del sistema. " +
            $"Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Application_types_following_the_interface_naming_convention_should_be_interfaces()
    {
        // "I" seguida de mayúscula es la convención de interfaz en .NET; nombres como
        // ImagePipelineContext empiezan por I pero no la siguen.
        var candidates = Types.InAssembly(ArchitectureLayers.Application)
            .That()
            .AreNotNested()
            .GetTypes()
            .Where(type => type.Name.Length > 1 && type.Name[0] == 'I' && char.IsUpper(type.Name[1]))
            .ToList();

        candidates.Should().NotBeEmpty("debería haber al menos un tipo I* en Application");
        candidates.Should().OnlyContain(
            type => type.IsInterface,
            "los tipos que siguen la convención I<Nombre> deben ser interfaces (DIP)");
    }

    [Fact]
    public void Application_types_should_not_use_System_Web()
    {
        var result = Types.InAssembly(ArchitectureLayers.Application)
            .ShouldNot()
            .HaveDependencyOn("System.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application layer must not depend on System.Web. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_types_should_not_use_System_Web()
    {
        var result = Types.InAssembly(ArchitectureLayers.Domain)
            .ShouldNot()
            .HaveDependencyOn("System.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Domain layer must not depend on System.Web. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}

public sealed class ProductionCodeHygieneTests
{
    [Fact]
    public void Production_code_should_not_use_Console()
    {
        var offenders = ArchitectureLayers.Production
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, IsNested: false })
            .Where(type => type
                .GetMethods(
                    System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Static
                    | System.Reflection.BindingFlags.Instance
                    | System.Reflection.BindingFlags.DeclaredOnly)
                .Any(method =>
                    method.DeclaringType == type
                    && method.Name is "WriteLine" or "Write" or "ReadLine" or "Read"
                    && method.GetParameters().Length <= 1))
            .Select(type => type.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "Console.WriteLine/ReadLine no se permiten en código de producción (usar ILogger o Serilog). " +
            $"Offenders: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void Only_the_web_layers_may_reference_AspNetCore()
    {
        var nonWebLayers = new[]
        {
            ArchitectureLayers.Shared,
            ArchitectureLayers.Domain,
            ArchitectureLayers.Application,
            ArchitectureLayers.Infrastructure,
        };

        var offenders = nonWebLayers
            .SelectMany(assembly => assembly
                .GetReferencedAssemblies()
                .Where(reference => reference.Name?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true)
                .Select(reference => $"{assembly.GetName().Name} -> {reference.Name}"))
            .ToList();

        offenders.Should().BeEmpty(
            "solo Shared.Web, Presentation y el host pueden depender de ASP.NET Core. " +
            $"Fails: {string.Join(", ", offenders)}");
    }
}

public sealed class SharedResultUsageTests
{
    [Fact]
    public void Result_type_should_only_live_in_Shared()
    {
        var result = Types.InAssemblies(
            [
                ArchitectureLayers.Domain,
                ArchitectureLayers.Application,
                ArchitectureLayers.Infrastructure,
                ArchitectureLayers.Presentation,
            ])
            .That()
            .HaveName("Result")
            .Should()
            .ResideInNamespace("LexPCImages.Shared.Common")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Result type must live in LexPCImages.Shared.Common. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Error_to_HTTP_mapping_should_live_only_in_Shared_Web()
    {
        // Antes el middleware del host y el controlador del módulo tenían cada uno su propio switch
        // sobre ErrorType y podían divergir. Se detecta cualquier método que reciba un ErrorType
        // y devuelva un código de estado fuera de LexPCImages.Shared.Web.
        const System.Reflection.BindingFlags AllMembers =
            System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.NonPublic
            | System.Reflection.BindingFlags.Static
            | System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.DeclaredOnly;

        static List<string> FindStatusCodeMappers(System.Reflection.Assembly assembly) => assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(AllMembers))
            .Where(method =>
                method.ReturnType == typeof(int)
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(ErrorType)))
            .Select(method => $"{method.DeclaringType?.FullName}.{method.Name}")
            .ToList();

        // El detector se autocomprueba: si dejara de encontrar el mapeador canónico, la regla
        // de abajo pasaría siempre y no protegería nada.
        FindStatusCodeMappers(ArchitectureLayers.SharedWeb).Should().ContainSingle(
            "LexPCImages.Shared.Web debe contener exactamente un mapeador de ErrorType a código HTTP");

        var offenders = new[] { ArchitectureLayers.Presentation, ArchitectureLayers.Host }
            .SelectMany(FindStatusCodeMappers)
            .ToList();

        offenders.Should().BeEmpty(
            "el mapeo de ErrorType a código HTTP debe estar solo en LexPCImages.Shared.Web. " +
            $"Offenders: {string.Join(", ", offenders)}");
    }
}
