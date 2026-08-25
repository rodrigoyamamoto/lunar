using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Core.Capabilities;

public sealed record CapabilityExecutionRequest
{
    public CapabilityId CapabilityId { get; }
    public AssetId AssetId { get; }
    public WorkflowExecutionId WorkflowExecutionId { get; }
    public WorkflowDefinitionId WorkflowDefinitionId { get; }
    public int WorkflowDefinitionVersion { get; }
    public int StepPosition { get; }


    public CapabilityExecutionRequest(
        CapabilityId capabilityId,
        AssetId assetId,
        WorkflowExecutionId workflowExecutionId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        int stepPosition)
    {
        if (capabilityId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Capability identifier cannot be empty.",
                nameof(capabilityId));
        }

        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        if (workflowExecutionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(workflowExecutionId));
        }

        if (workflowDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow definition identifier cannot be empty.",
                nameof(workflowDefinitionId));
        }

        if (workflowDefinitionVersion < 1)
        {
            throw new ArgumentException(
                "Workflow definition version must be a positive integer.",
                nameof(workflowDefinitionVersion));
        }

        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        CapabilityId = capabilityId;
        AssetId = assetId;
        WorkflowExecutionId = workflowExecutionId;
        WorkflowDefinitionId = workflowDefinitionId;
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        StepPosition = stepPosition;
    }
}
