namespace Lunar.Core.Workflows;

public sealed class WorkflowExecution
{
    private WorkflowExecution(
        WorkflowExecutionId id,
        DateTimeOffset createdAt)
    {
        Id = id;
        CreatedAt = createdAt;
        Status = WorkflowExecutionStatus.Created;
    }

    public WorkflowExecutionId Id { get; }

    public WorkflowExecutionStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }


    public static WorkflowExecution Create()
    {
        return new WorkflowExecution(
            WorkflowExecutionId.New(),
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