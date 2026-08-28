using LexPCImages.Shared.Common.Errors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LexPCImages.Shared.Web.Http;

/// <summary>
/// Única fuente de verdad para traducir un <see cref="Error"/> del dominio a HTTP.
/// La usan tanto los controladores de los módulos como el middleware global del host,
/// de modo que la respuesta de error es idéntica venga de donde venga.
/// </summary>
public static class ErrorHttpMapper
{
    /// <summary>RFC 9110, sección 15: "Response Status Codes".</summary>
    public const string ProblemTypeUri = "https://datatracker.ietf.org/doc/html/rfc9110#section-15";

    /// <summary>Media type de RFC 9457 para respuestas de error.</summary>
    public const string ProblemContentType = "application/problem+json";

    public static int ToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.DependencyFailure => StatusCodes.Status502BadGateway,
        ErrorType.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorType.Internal => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    /// <summary>
    /// Resultado listo para devolver desde un controlador, con el <c>Content-Type</c> correcto:
    /// <c>StatusCode(code, problem)</c> serializaba como <c>application/json</c> y no coincidía
    /// con lo que emite el middleware global.
    /// </summary>
    public static ObjectResult ToProblemResult(Error error, string? instance = null)
    {
        var problem = ToProblemDetails(error, instance);
        var result = new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { ProblemContentType },
        };
        return result;
    }

    public static ProblemDetails ToProblemDetails(Error error, string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        var statusCode = ToStatusCode(error.Type);
        var problem = new ProblemDetails
        {
            Type = ProblemTypeUri,
            Title = error.Type.ToString(),
            Status = statusCode,
            Detail = error.Message,
            Instance = instance,
        };
        problem.Extensions["code"] = error.Code;
        if (error.Details is { Count: > 0 })
        {
            problem.Extensions["errors"] = error.Details;
        }
        return problem;
    }
}
