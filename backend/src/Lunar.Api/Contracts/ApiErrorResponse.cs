namespace Lunar.Api.Contracts;

public sealed class ApiErrorResponse
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public int? RetryAfterSeconds { get; init; }
}
