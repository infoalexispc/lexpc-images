using System.Text.Json;
using LexPCImages.Shared.Common.Errors;

namespace LexPCImages.API.Middleware;

public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !context.RequestAborted.IsCancellationRequested)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            var error = MapException(ex);
            await WriteProblemAsync(context, error);
        }
    }

    private static Error MapException(Exception ex) => ex switch
    {
        ArgumentException ae => Error.Validation("argument.invalid", ae.Message),
        KeyNotFoundException knf => Error.NotFound("resource.not_found", knf.Message),
        InvalidOperationException ioe => Error.Conflict("operation.invalid", ioe.Message),
        NotImplementedException => Error.Internal("not.implemented", "The requested operation is not implemented."),
        TimeoutException => Error.DependencyFailure("dependency.timeout", ex.Message),
        _ => Error.Internal("internal.error", "An unexpected error occurred."),
    };

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.DependencyFailure => StatusCodes.Status502BadGateway,
        ErrorType.Internal => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    private static async Task WriteProblemAsync(HttpContext context, Error error)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodeFor(error.Type);
        context.Response.ContentType = "application/problem+json";

        var problem = new Dictionary<string, object?>
        {
            ["type"] = $"https://datatracker.ietf.org/doc/html/rfc9110#section-15",
            ["title"] = error.Type.ToString(),
            ["status"] = StatusCodeFor(error.Type),
            ["code"] = error.Code,
            ["detail"] = error.Message,
            ["instance"] = context.Request.Path.HasValue ? context.Request.Path.Value : null,
        };

        if (error.Details is { Count: > 0 })
        {
            problem["errors"] = error.Details;
        }

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
