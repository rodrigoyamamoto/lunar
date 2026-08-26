namespace Lunar.Core.Capabilities;

public sealed record CapabilityExecutionSucceeded : CapabilityExecutionOutcome
{
    public CapabilityExecutionOutput Output { get; }


    public CapabilityExecutionSucceeded(CapabilityExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        Output = output;
    }
}
