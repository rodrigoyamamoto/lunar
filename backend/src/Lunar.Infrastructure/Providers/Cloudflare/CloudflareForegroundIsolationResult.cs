using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers.Cloudflare;

internal abstract record CloudflareForegroundIsolationResult
{
    private protected CloudflareForegroundIsolationResult() { }
}

internal sealed record CloudflareForegroundIsolationSucceeded(byte[] PngBytes)
    : CloudflareForegroundIsolationResult;

internal sealed record CloudflareForegroundIsolationFailed(
    CloudflareForegroundIsolationFailure Failure)
    : CloudflareForegroundIsolationResult;

internal sealed record CloudflareForegroundIsolationFailure(
    CapabilityExecutionFailureKind Kind,
    TimeSpan? RetryAfter);
