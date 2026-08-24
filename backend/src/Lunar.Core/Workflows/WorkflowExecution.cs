using Lunar.Core.Assets;

namespace Lunar.Core.Workflows;

public sealed class WorkflowExecution
{
    private WorkflowExecution(
        WorkflowExecutionId id,
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        DateTimeOffset createdAt)
    {
        Id = id;
        AssetId = assetId;
        WorkflowDefinitionId = workflowDefinitionId;
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        CreatedAt = createdAt;
        Status = WorkflowExecutionStatus.Created;
    }

    public WorkflowExecutionId Id { get; }

    public AssetId AssetId { get; }

    public WorkflowDefinitionId WorkflowDefinitionId { get; }

    public int WorkflowDefinitionVersion { get; }

    public WorkflowExecutionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }


    public static WorkflowExecution Create(
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
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

        return new WorkflowExecution(
            WorkflowExecutionId.New(),
            assetId,
            workflowDefinitionId,
            workflowDefinitionVersion,
            DateTimeOffset.UtcNow);
    }


    public void Start()
    {
        if (Status != WorkflowExecutionStatus.Created)
        {
            return;
        }

        Status = WorkflowExecutionStatus.Running;
        StartedAt = DateTimeOffset.UtcNow;
    }


    public void Complete()
    {
        if (Status != WorkflowExecutionStatus.Running)
        {
            return;
        }

        Status = WorkflowExecutionStatus.Completed;
        CompletedAt = DateTimeOffset.UtcNow;
    }


    public void Fail()
    {
        if (Status != WorkflowExecutionStatus.Running)
        {
            return;
        }

        Status = WorkflowExecutionStatus.Failed;
        CompletedAt = DateTimeOffset.UtcNow;
    }


    public void Cancel()
    {
        if (Status != WorkflowExecutionStatus.Running)
        {
            return;
        }

        Status = WorkflowExecutionStatus.Cancelled;
        CompletedAt = DateTimeOffset.UtcNow;
    }
}