using System.Net.Http.Headers;
using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareForegroundIsolationClient
{
    private const int MaxInputBytes = 20 * 1024 * 1024; // 20 MB (Cloudflare Images binding limit)

    private readonly HttpClient _httpClient;
    private readonly CloudflareForegroundIsolationConfiguration _configuration;

    public CloudflareForegroundIsolationClient(
        HttpClient httpClient,
        CloudflareForegroundIsolationConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);

        _httpClient = httpClient;
        _configuration = configuration;
    }


    internal async Task<CloudflareForegroundIsolationResult> IsolateForegroundAsync(
        byte[] imageBytes,
        string mediaType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageBytes);

        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
        }

        if (imageBytes.Length > MaxInputBytes)
        {
            return Fail(CapabilityExecutionFailureKind.Rejected, null);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        linkedCts.CancelAfter(_configuration.RequestTimeout);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _configuration.Endpoint);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _configuration.ServiceToken);
        httpRequest.Content = new ByteArrayContent(imageBytes);
        httpRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fail(CapabilityExecutionFailureKind.TimedOut, null);
        }
        catch (HttpRequestException)
        {
            return Fail(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, null);
        }

        using (response)
        {
            return await ParseResponseAsync(response, linkedCts.Token, cancellationToken);
        }
    }


    private static async Task<CloudflareForegroundIsolationResult> ParseResponseAsync(
        HttpResponseMessage response,
        CancellationToken linkedToken,
        CancellationToken cancellationToken)
    {
        var retryAfter = ParseRetryAfter(response.Headers);

        if (!response.IsSuccessStatusCode)
        {
            return Fail(ClassifyByHttpStatus(response.StatusCode), retryAfter);
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;

        if (!string.Equals(contentType, "image/png", StringComparison.OrdinalIgnoreCase))
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        byte[] pngBytes;

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync(linkedToken);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, linkedToken);
            pngBytes = memory.ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Fail(CapabilityExecutionFailureKind.TimedOut, retryAfter);
        }
        catch (IOException)
        {
            return Fail(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, retryAfter);
        }

        if (pngBytes.Length == 0)
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        if (!HasPngSignature(pngBytes))
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        return new CloudflareForegroundIsolationSucceeded(pngBytes);
    }


    internal static CapabilityExecutionFailureKind ClassifyByHttpStatus(System.Net.HttpStatusCode status)
    {
        var code = (int)status;

        if (code == 401) return CapabilityExecutionFailureKind.AuthenticationFailed;
        if (code == 403) return CapabilityExecutionFailureKind.AccessDenied;
        if (code == 408) return CapabilityExecutionFailureKind.TimedOut;
        if (code == 429) return CapabilityExecutionFailureKind.RateLimited;
        if (code >= 400 && code < 500) return CapabilityExecutionFailureKind.Rejected;
        if (code >= 500 && code < 600) return CapabilityExecutionFailureKind.TemporarilyUnavailable;

        return CapabilityExecutionFailureKind.InvalidResponse;
    }


    private static TimeSpan? ParseRetryAfter(System.Net.Http.Headers.HttpResponseHeaders headers)
    {
        if (!headers.TryGetValues("Retry-After", out var values))
        {
            return null;
        }

        var raw = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (int.TryParse(raw, out var seconds) && seconds > 0)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        return null;
    }


    private static bool HasPngSignature(byte[] bytes)
    {
        // PNG signature: 89 50 4E 47 0D 0A 1A 0A
        return bytes.Length >= 8 &&
               bytes[0] == 0x89 &&
               bytes[1] == 0x50 &&
               bytes[2] == 0x4E &&
               bytes[3] == 0x47 &&
               bytes[4] == 0x0D &&
               bytes[5] == 0x0A &&
               bytes[6] == 0x1A &&
               bytes[7] == 0x0A;
    }


    private static CloudflareForegroundIsolationResult Fail(
        CapabilityExecutionFailureKind kind,
        TimeSpan? retryAfter)
    {
        return new CloudflareForegroundIsolationFailed(
            new CloudflareForegroundIsolationFailure(kind, retryAfter));
    }
}
