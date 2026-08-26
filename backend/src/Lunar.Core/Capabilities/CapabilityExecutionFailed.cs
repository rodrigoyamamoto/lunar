namespace Lunar.Core.Capabilities;

public sealed record CapabilityExecutionFailed : CapabilityExecutionOutcome
{
    public CapabilityExecutionFailure Failure { get; }


    public CapabilityExecutionFailed(CapabilityExecutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        Failure = failure;
    }
}
