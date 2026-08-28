namespace LexPCImages.Shared.Common.Errors;

public sealed record Error(ErrorType Type, string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null)
{
    public static Error Validation(string code, string message, IReadOnlyDictionary<string, object?>? details = null)
        => new(ErrorType.Validation, code, message, details);

    public static Error NotFound(string code, string message)
        => new(ErrorType.NotFound, code, message);

    public static Error Conflict(string code, string message)
        => new(ErrorType.Conflict, code, message);

    public static Error Unauthorized(string code, string message)
        => new(ErrorType.Unauthorized, code, message);

    public static Error Forbidden(string code, string message)
        => new(ErrorType.Forbidden, code, message);

    public static Error DependencyFailure(string code, string message)
        => new(ErrorType.DependencyFailure, code, message);

    public static Error Unavailable(string code, string message)
        => new(ErrorType.Unavailable, code, message);

    public static Error Internal(string code, string message)
        => new(ErrorType.Internal, code, message);
}
