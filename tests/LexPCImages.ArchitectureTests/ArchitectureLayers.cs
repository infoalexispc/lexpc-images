using System.Reflection;

namespace LexPCImages.ArchitectureTests;

/// <summary>Ensamblados de cada capa, para no repetir <c>typeof(...).Assembly</c> en cada regla.</summary>
internal static class ArchitectureLayers
{
    public static Assembly Shared { get; } = typeof(LexPCImages.Shared.Common.Result).Assembly;

    public static Assembly SharedWeb { get; } = typeof(LexPCImages.Shared.Web.Http.ErrorHttpMapper).Assembly;

    public static Assembly Domain { get; } = typeof(LexPCImages.Modules.Optimizer.Domain.ValueObjects.SlotId).Assembly;

    public static Assembly Application { get; } =
        typeof(LexPCImages.Modules.Optimizer.Application.Abstractions.IImageResizer).Assembly;

    public static Assembly Infrastructure { get; } =
        typeof(LexPCImages.Modules.Optimizer.Infrastructure.AssemblyMarker).Assembly;

    public static Assembly Presentation { get; } =
        typeof(LexPCImages.Modules.Optimizer.Presentation.OptimizerModule).Assembly;

    public static Assembly Host { get; } = typeof(LexPCImages.API.Middleware.GlobalExceptionMiddleware).Assembly;

    public static Assembly[] Production { get; } =
        [Shared, SharedWeb, Domain, Application, Infrastructure, Presentation, Host];
}
