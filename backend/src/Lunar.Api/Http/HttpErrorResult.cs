using Lunar.Api.Contracts;

namespace Lunar.Api.Http;

public sealed record HttpErrorResult(
    int StatusCode,
    ApiErrorResponse Response,
    int? RetryAfterSeconds = null);
