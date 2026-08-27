using System.Linq;
using FluentAssertions;
using LexPCImages.Shared.Common;
using LexPCImages.Shared.Common.Errors;
using NetArchTest.Rules;

namespace LexPCImages.ArchitectureTests;

public sealed class DddConventionTests
{
    [Fact]
    public void Domain_classes_named_Entity_should_reside_in_Domain_namespace()
    {
        var types = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId).Assembly)
            .That()
            .HaveNameEndingWith("Entity")
            .Should()
            .ResideInNamespace("LexPCImages.Modules.Optimizer.Domain")
            .GetResult();

        types.IsSuccessful.Should().BeTrue(
            $"Entity types must live in LexPCImages.Modules.Optimizer.Domain. Fails: {string.Join(", ", types.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_classes_named_Job_should_have_no_public_setters_on_state_properties()
    {
        // Aggregates del dominio: las propiedades de estado (Status, Progress, CurrentStage, Output*, Error*)
        // se modifican con métodos (MarkProcessing, MarkDone, MarkError, UpdateProgress), no con setters públicos.
        var processJob = typeof(LexPCImages.Modules.Optimizer.Domain.Entities.ProcessJob);
        var statePropertyNames = new[] { "Status", "CurrentStage", "Progress", "OutputImage", "OutputContentType", "ErrorMessage", "StartedAt", "CompletedAt" };

        var publicSetters = statePropertyNames
            .Select(name => processJob.GetProperty(name))
            .Where(p => p is not null && p.GetSetMethod() is { IsPublic: true })
            .Select(p => p!.Name)
            .ToList();

        publicSetters.Should().BeEmpty(
            $"ProcessJob must expose state changes only through methods, not public setters. " +
            $"Offenders: {string.Join(", ", publicSetters)}");
    }

    [Fact]
    public void Application_types_starting_with_I_should_be_interfaces()
    {
        var types = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IBackgroundRemovalService).Assembly)
            .That()
            .HaveNameStartingWith("I")
            .And()
            .AreNotNested()
            .GetTypes();

        types.Should().NotBeEmpty("debería haber al menos un tipo I* en Application");
        types.Should().OnlyContain(t => t.IsInterface, "todos los tipos con nombre que empieza por I deben ser interfaces (DIP)");
    }

    [Fact]
    public void Application_types_should_not_use_System_Web()
    {
        var result = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IBackgroundRemovalService).Assembly)
            .ShouldNot()
            .HaveDependencyOn("System.Web")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Application layer must not depend on System.Web. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_types_should_not_use_System_Web()
    {
        var result = Types.InAssembly(typeof(LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId).Assembly)
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
        var assemblies = new[]
        {
            typeof(LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId).Assembly,
            typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IBackgroundRemovalService).Assembly,
            typeof(LexPCImages.Modules.Optimizer.Infrastructure.AssemblyMarker).Assembly,
            typeof(LexPCImages.Modules.Optimizer.Presentation.OptimizerModule).Assembly,
            typeof(LexPCImages.Shared.IModuleRegistration).Assembly,
            typeof(LexPCImages.API.Middleware.GlobalExceptionMiddleware).Assembly,
        };

        var offenders = assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && !t.IsNested)
            .Where(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)
                .Any(m => m.DeclaringType == t &&
                          (m.Name == "WriteLine" || m.Name == "Write" || m.Name == "ReadLine" || m.Name == "Read") &&
                          m.GetParameters().Length <= 1))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty(
            "Console.WriteLine/ReadLine no se permiten en código de producción (usar ILogger o Serilog). Offenders: " +
            string.Join(", ", offenders));
    }
}

public sealed class SharedResultUsageTests
{
    [Fact]
    public void Result_type_should_only_live_in_Shared()
    {
        var result = Types.InAssemblies(new[]
            {
                typeof(LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId).Assembly,
                typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IBackgroundRemovalService).Assembly,
                typeof(LexPCImages.Modules.Optimizer.Infrastructure.AssemblyMarker).Assembly,
                typeof(LexPCImages.Modules.Optimizer.Presentation.OptimizerModule).Assembly,
            })
            .That()
            .HaveName("Result")  // exactamente "Result", no el genérico
            .Should()
            .ResideInNamespace("LexPCImages.Shared.Common")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            $"Result type must live in LexPCImages.Shared.Common. Fails: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
