using Lunar.Core.Primitives;

namespace Lunar.Core.Workflows;

public readonly record struct WorkflowDefinitionId(Guid Value)
{
    public static WorkflowDefinitionId New()
    {
        return new WorkflowDefinitionId(IdGenerator.New());
    }
}
