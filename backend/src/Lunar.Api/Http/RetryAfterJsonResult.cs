using System.Text.Json;
using Lunar.Api.Contracts;

namespace Lunar.Api.Http;

internal sealed class RetryAfterJsonResult : IResult
{
    private readonly ApiErrorResponse _response;
    private readonly int _statusCode;
    private readonly int _retryAfterSeconds;

    public RetryAfterJsonResult(ApiErrorResponse response, int statusCode, int retryAfterSeconds)
    {
        _response = response;
        _statusCode = statusCode;
        _retryAfterSeconds = retryAfterSeconds;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = _statusCode;
        httpContext.Response.Headers["Retry-After"] = _retryAfterSeconds.ToString();
        return httpContext.Response.WriteAsJsonAsync(_response, _response.GetType(), httpContext.RequestAborted);
    }
}
