using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Api;

public sealed class TrackingWorkflowExecutionRepository : IWorkflowExecutionRepository
{
    private readonly InMemoryWorkflowExecutionRepository _inner = new();

    public int TryAddCallCount { get; private set; }

    public int TryUpdateCallCount { get; private set; }


    public Task<bool> TryAddAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken = default)
    {
        TryAddCallCount++;
        return _inner.TryAddAsync(execution, cancellationToken);
    }

    public Task<WorkflowExecution?> GetAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken = default)
    {
        return _inner.GetAsync(id, cancellationToken);
    }

    public Task<WorkflowExecution?> TryUpdateAsync(
        WorkflowExecution execution,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        TryUpdateCallCount++;
        return _inner.TryUpdateAsync(execution, expectedRevision, cancellationToken);
    }


    public void Reset()
    {
        TryAddCallCount = 0;
        TryUpdateCallCount = 0;
    }
}
