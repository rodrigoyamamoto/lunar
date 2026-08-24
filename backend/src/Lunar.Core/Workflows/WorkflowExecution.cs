using Lunar.Core.Assets;

namespace Lunar.Core.Workflows;

public sealed class WorkflowExecution
{
    private WorkflowExecution(
        WorkflowExecutionId id,
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        WorkflowExecutionStatus status,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        Id = id;
        AssetId = assetId;
        WorkflowDefinitionId = workflowDefinitionId;
        WorkflowDefinitionVersion = workflowDefinitionVersion;
        Status = status;
        Revision = revision;
        CreatedAt = createdAt;
        StartedAt = startedAt;
        CompletedAt = completedAt;
    }

    public WorkflowExecutionId Id { get; }

    public AssetId AssetId { get; }

    public WorkflowDefinitionId WorkflowDefinitionId { get; }

    public int WorkflowDefinitionVersion { get; }

    public WorkflowExecutionStatus Status { get; private set; }

    public long Revision { get; }

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
            WorkflowExecutionStatus.Created,
            0,
            DateTimeOffset.UtcNow,
            null,
            null);
    }


    public static WorkflowExecution Rehydrate(
        WorkflowExecutionId id,
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        WorkflowExecutionStatus status,
        long revision,
        DateTimeOffset createdAt,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(id));
        }

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

        if (revision < 0)
        {
            throw new ArgumentException(
                "Workflow execution revision cannot be negative.",
                nameof(revision));
        }

        ValidateStateCoherence(status, startedAt, completedAt);

        return new WorkflowExecution(
            id,
            assetId,
            workflowDefinitionId,
            workflowDefinitionVersion,
            status,
            revision,
            createdAt,
            startedAt,
            completedAt);
    }


    private static void ValidateStateCoherence(
        WorkflowExecutionStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt)
    {
        switch (status)
        {
            case WorkflowExecutionStatus.Created:
                if (startedAt is not null || completedAt is not null)
                {
                    throw new ArgumentException(
                        "Created execution must not have StartedAt or CompletedAt.",
                        nameof(status));
                }
                break;

            case WorkflowExecutionStatus.Running:
                if (startedAt is null || completedAt is not null)
                {
                    throw new ArgumentException(
                        "Running execution must have StartedAt and no CompletedAt.",
                        nameof(status));
                }
                break;

            case WorkflowExecutionStatus.Completed:
            case WorkflowExecutionStatus.Failed:
            case WorkflowExecutionStatus.Cancelled:
                if (startedAt is null || completedAt is null)
                {
                    throw new ArgumentException(
                        "Terminal execution must have both StartedAt and CompletedAt.",
                        nameof(status));
                }
                break;

            default:
                throw new ArgumentException(
                    "Unknown workflow execution status.",
                    nameof(status));
        }
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
