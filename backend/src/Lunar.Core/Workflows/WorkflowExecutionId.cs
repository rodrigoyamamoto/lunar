namespace Lunar.Core.Workflows;

public readonly record struct WorkflowExecutionId(Guid Value)
{
    public static WorkflowExecutionId New()
    {
        return new WorkflowExecutionId(Guid.CreateVersion7());
    }
}