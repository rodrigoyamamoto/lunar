using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers.Cloudflare;

internal abstract record CloudflareImageGenerationResult
{
    private protected CloudflareImageGenerationResult() { }
}

internal sealed record CloudflareImageGenerationSucceeded(byte[] ImageBytes)
    : CloudflareImageGenerationResult;

internal sealed record CloudflareImageGenerationFailed(
    CloudflareImageGenerationFailure Failure)
    : CloudflareImageGenerationResult;

internal sealed record CloudflareImageGenerationFailure(
    CapabilityExecutionFailureKind Kind,
    TimeSpan? RetryAfter);
