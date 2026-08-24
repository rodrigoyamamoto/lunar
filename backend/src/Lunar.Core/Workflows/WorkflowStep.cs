using Lunar.Core.Capabilities;

namespace Lunar.Core.Workflows;

public readonly record struct WorkflowStep
{
    public int Position { get; }

    public CapabilityId CapabilityId { get; }


    public WorkflowStep(
        int position,
        CapabilityId capabilityId)
    {
        if (position <= 0)
        {
            throw new ArgumentException(
                "Workflow step position must be positive.",
                nameof(position));
        }

        if (capabilityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow step capability identifier cannot be empty.",
                nameof(capabilityId));
        }

        Position = position;
        CapabilityId = capabilityId;
    }
}
