using Lunar.Core.Workflows;

namespace Lunar.Application.Artifacts;

public sealed record GeneratedArtifact
{
    public WorkflowExecutionId WorkflowExecutionId { get; }

    public ProducedArtifact ProducedArtifact { get; }


    public GeneratedArtifact(
        WorkflowExecutionId workflowExecutionId,
        ProducedArtifact producedArtifact)
    {
        if (workflowExecutionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(workflowExecutionId));
        }

        ArgumentNullException.ThrowIfNull(producedArtifact);

        WorkflowExecutionId = workflowExecutionId;
        ProducedArtifact = producedArtifact;
    }
}
