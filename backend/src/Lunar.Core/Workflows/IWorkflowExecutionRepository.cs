namespace Lunar.Core.Workflows;

public interface IWorkflowExecutionRepository
{
    Task<bool> TryAddAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecution?> GetAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken = default);

    Task<WorkflowExecution?> TryUpdateAsync(
        WorkflowExecution execution,
        long expectedRevision,
        CancellationToken cancellationToken = default);
}
