using Lunar.Application.Errors;

namespace Lunar.Application;

public sealed class Result<T>
{
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public T? Value { get; }

    public ApplicationError? Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = null;
    }

    private Result(ApplicationError error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value)
    {
        return new Result<T>(value);
    }

    public static Result<T> Failure(ApplicationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new Result<T>(error);
    }
}
