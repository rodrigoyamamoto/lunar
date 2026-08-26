namespace Lunar.Core.Capabilities;

public sealed record CapabilityExecutionFailure
{
    public CapabilityExecutionFailureKind Kind { get; }

    public TimeSpan? RetryAfter { get; }


    public CapabilityExecutionFailure(
        CapabilityExecutionFailureKind kind,
        TimeSpan? retryAfter = null)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(kind),
                "Capability execution failure kind is not a defined enum value.");
        }

        if (retryAfter is { } duration && duration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryAfter),
                "RetryAfter must be strictly greater than zero.");
        }

        Kind = kind;
        RetryAfter = retryAfter;
    }
}
