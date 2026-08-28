using System.Text.Json;
using LexPCImages.Shared.Common.Errors;
using LexPCImages.Shared.Web.Http;

namespace LexPCImages.API.Middleware;

/// <summary>
/// Red de seguridad del host. Una excepción que llega hasta aquí es un fallo no previsto, así
/// que se responde siempre <c>500</c> con un mensaje genérico: los errores esperables viajan
/// como <c>Result</c> desde los casos de uso y ya los traduce el controlador.
/// <para>
/// La versión anterior mapeaba <see cref="ArgumentException"/> a <c>400</c> y
/// <see cref="InvalidOperationException"/> a <c>409</c> devolviendo el mensaje de la excepción:
/// presentaba bugs internos como errores del cliente y filtraba detalles de implementación.
/// </para>
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Error UnexpectedError = Error.Internal(
        "internal.error",
        "An unexpected error occurred.");

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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // El cliente cortó la conexión: no es un error del servidor y no hay a quién responder.
            _logger.LogDebug(
                "Request {Method} {Path} aborted by the client",
                context.Request.Method,
                context.Request.Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteProblemAsync(context);
        }
    }

    private static async Task WriteProblemAsync(HttpContext context)
    {
        var problem = ErrorHttpMapper.ToProblemDetails(UnexpectedError, context.Request.Path);

        context.Response.Clear();
        context.Response.StatusCode = problem.Status!.Value;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, JsonOptions),
            context.RequestAborted);
    }
}
