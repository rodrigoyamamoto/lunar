namespace Lunar.Core.Capabilities;

public enum CapabilityExecutionFailureKind
{
    Rejected,
    AuthenticationFailed,
    AccessDenied,
    QuotaExhausted,
    RateLimited,
    PaidPlanRequired,
    TimedOut,
    TemporarilyUnavailable,
    RemoteOutcomeUnknown,
    InvalidResponse
}
