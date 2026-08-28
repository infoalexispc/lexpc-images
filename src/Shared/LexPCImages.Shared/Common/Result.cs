using System.Diagnostics.CodeAnalysis;
using LexPCImages.Shared.Common.Errors;

namespace LexPCImages.Shared.Common;

/// <summary>
/// Estado interno del resultado. <see cref="Uninitialized"/> es el valor por defecto del struct y
/// permite detectar un <c>default(Result)</c> en lugar de confundirlo con un fallo sin error.
/// </summary>
internal enum ResultState
{
    Uninitialized = 0,
    Success = 1,
    Failure = 2,
}

public readonly struct Result
{
    private readonly ResultState _state;
    private readonly Error? _error;

    private Result(ResultState state, Error? error)
    {
        _state = state;
        _error = error;
    }

    public bool IsSuccess => EnsureInitialized() == ResultState.Success;

    public bool IsFailure => !IsSuccess;

    public Error? Error => EnsureInitialized() == ResultState.Success ? null : _error;

    public static Result Success() => new(ResultState.Success, null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(ResultState.Failure, error);
    }

    public static Result Failure(ErrorType type, string code, string message)
        => Failure(new Error(type, code, message));

    /// <summary>Devuelve el error o lanza si el resultado es correcto. Evita el operador <c>!</c> en las llamadas.</summary>
    public Error ErrorOrThrow() => Error
        ?? throw new InvalidOperationException("The result is successful; there is no error to read.");

    public static implicit operator Result(Error error) => Failure(error);

    private ResultState EnsureInitialized() => _state != ResultState.Uninitialized
        ? _state
        : throw new InvalidOperationException(
            "default(Result) is not a valid result. Build it with Result.Success() or Result.Failure(...).");
}

public readonly struct Result<T>
{
    private readonly ResultState _state;
    private readonly T? _value;
    private readonly Error? _error;

    private Result(ResultState state, T? value, Error? error)
    {
        _state = state;
        _value = value;
        _error = error;
    }

    public bool IsSuccess => EnsureInitialized() == ResultState.Success;

    public bool IsFailure => !IsSuccess;

    public T? Value => EnsureInitialized() == ResultState.Success ? _value : default;

    public Error? Error => EnsureInitialized() == ResultState.Success ? null : _error;

    public static Result<T> Success(T value) => new(ResultState.Success, value, null);

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(ResultState.Failure, default, error);
    }

    public static Result<T> Failure(ErrorType type, string code, string message)
        => Failure(new Error(type, code, message));

    /// <summary>Devuelve el valor o lanza si el resultado es un fallo. Evita el operador <c>!</c> en las llamadas.</summary>
    public T ValueOrThrow() => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            $"The result is a failure ({_error?.Code}): {_error?.Message}");

    /// <summary>Devuelve el error o lanza si el resultado es correcto. Evita el operador <c>!</c> en las llamadas.</summary>
    public Error ErrorOrThrow() => Error
        ?? throw new InvalidOperationException("The result is successful; there is no error to read.");

    public bool TryGetValue([NotNullWhen(true)] out T? value)
    {
        value = IsSuccess ? _value : default;
        return value is not null;
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    private ResultState EnsureInitialized() => _state != ResultState.Uninitialized
        ? _state
        : throw new InvalidOperationException(
            "default(Result<T>) is not a valid result. Build it with Result<T>.Success(...) or Result<T>.Failure(...).");
}
