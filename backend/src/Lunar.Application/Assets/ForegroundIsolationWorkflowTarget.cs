using Lunar.Core.Workflows;

namespace Lunar.Application.Assets;

/// <summary>
/// Application value object for the configured foreground-isolation
/// workflow target. Mirrors <see cref="Lunar.Application.Workflows.GenerationWorkflowTarget"/>
/// but for the background-removal product operation.
/// </summary>
public sealed record ForegroundIsolationWorkflowTarget
{
    public WorkflowDefinitionId WorkflowDefinitionId { get; }

    public int Version { get; }

    public int StepPosition { get; }


    public ForegroundIsolationWorkflowTarget(
        WorkflowDefinitionId workflowDefinitionId,
        int version,
        int stepPosition)
    {
        if (workflowDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow definition identifier cannot be empty.",
                nameof(workflowDefinitionId));
        }

        if (version < 1)
        {
            throw new ArgumentException(
                "Workflow definition version must be a positive integer.",
                nameof(version));
        }

        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        WorkflowDefinitionId = workflowDefinitionId;
        Version = version;
        StepPosition = stepPosition;
    }
}
