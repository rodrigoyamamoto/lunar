using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers.Cloudflare;

internal static class CloudflareWorkersAiResponseParser
{
    private static readonly byte[] JpegSignature = { 0xFF, 0xD8, 0xFF };


    public static async Task<CloudflareImageGenerationResult> ParseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var retryAfter = ParseRetryAfter(response.Headers);

        CloudflareResponseEnvelope? envelope;

        try
        {
            envelope = await response.Content.ReadFromJsonAsync<CloudflareResponseEnvelope>(
                cancellationToken);
        }
        catch (JsonException)
        {
            return ClassifyByEnvelopeOrStatus(null, response.StatusCode, retryAfter, cancellationToken);
        }

        return ClassifyByEnvelopeOrStatus(envelope, response.StatusCode, retryAfter, cancellationToken);
    }


    internal static CapabilityExecutionFailureKind ClassifyByHttpStatus(HttpStatusCode status)
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


    private static CloudflareImageGenerationResult ClassifyByEnvelopeOrStatus(
        CloudflareResponseEnvelope? envelope,
        HttpStatusCode status,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        var isSuccess = (int)status >= 200 && (int)status < 300;

        if (envelope is null)
        {
            if (isSuccess)
            {
                return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
            }

            return Fail(ClassifyByHttpStatus(status), retryAfter);
        }

        if (envelope.Success is not true)
        {
            var errorKind = MapErrors(envelope.Errors);

            if (errorKind is not null)
            {
                return Fail(errorKind.Value, retryAfter);
            }

            if (isSuccess)
            {
                return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
            }

            return Fail(ClassifyByHttpStatus(status), retryAfter);
        }

        if (!isSuccess)
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        return ValidateSuccessPayload(envelope, retryAfter, cancellationToken);
    }


    private static CloudflareImageGenerationResult ValidateSuccessPayload(
        CloudflareResponseEnvelope envelope,
        TimeSpan? retryAfter,
        CancellationToken cancellationToken)
    {
        if (envelope.Result is null)
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        var base64Image = envelope.Result.Image;

        if (string.IsNullOrWhiteSpace(base64Image))
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        cancellationToken.ThrowIfCancellationRequested();

        byte[] imageBytes;

        try
        {
            imageBytes = Convert.FromBase64String(base64Image);
        }
        catch (FormatException)
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (imageBytes.Length == 0)
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        if (!HasJpegSignature(imageBytes))
        {
            return Fail(CapabilityExecutionFailureKind.InvalidResponse, retryAfter);
        }

        return new CloudflareImageGenerationSucceeded(imageBytes);
    }


    private static CapabilityExecutionFailureKind? MapErrors(List<CloudflareError>? errors)
    {
        if (errors is { Count: > 0 })
        {
            foreach (var error in errors)
            {
                var mapped = MapInternalCode(error.Code);

                if (mapped is not null)
                {
                    return mapped;
                }
            }
        }

        return null;
    }


    private static CapabilityExecutionFailureKind? MapInternalCode(int code)
    {
        return code switch
        {
            3036 => CapabilityExecutionFailureKind.QuotaExhausted,
            3040 => CapabilityExecutionFailureKind.TemporarilyUnavailable,
            5035 => CapabilityExecutionFailureKind.PaidPlanRequired,
            3007 => CapabilityExecutionFailureKind.TimedOut,
            3008 => CapabilityExecutionFailureKind.TimedOut,
            _ => null
        };
    }


    private static TimeSpan? ParseRetryAfter(HttpResponseHeaders headers)
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

        if (DateTimeOffset.TryParse(raw, out var date))
        {
            var delta = date - DateTimeOffset.UtcNow;

            if (delta > TimeSpan.Zero)
            {
                return delta;
            }
        }

        return null;
    }


    private static bool HasJpegSignature(byte[] bytes)
    {
        return bytes.Length >= JpegSignature.Length &&
               bytes[0] == JpegSignature[0] &&
               bytes[1] == JpegSignature[1] &&
               bytes[2] == JpegSignature[2];
    }


    private static CloudflareImageGenerationResult Fail(
        CapabilityExecutionFailureKind kind,
        TimeSpan? retryAfter)
    {
        return new CloudflareImageGenerationFailed(
            new CloudflareImageGenerationFailure(kind, retryAfter));
    }


    private sealed class CloudflareResponseEnvelope
    {
        [JsonPropertyName("success")]
        public bool? Success { get; set; }

        [JsonPropertyName("result")]
        public CloudflareResult? Result { get; set; }

        [JsonPropertyName("errors")]
        public List<CloudflareError>? Errors { get; set; }
    }

    private sealed class CloudflareResult
    {
        [JsonPropertyName("image")]
        public string? Image { get; set; }
    }

    private sealed class CloudflareError
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
