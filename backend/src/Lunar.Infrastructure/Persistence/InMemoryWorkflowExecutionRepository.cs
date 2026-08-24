using System.Collections.Concurrent;
using Lunar.Core.Workflows;

namespace Lunar.Infrastructure.Persistence;

public sealed class InMemoryWorkflowExecutionRepository : IWorkflowExecutionRepository
{
    private readonly ConcurrentDictionary<WorkflowExecutionId, WorkflowExecution> _store = new();

    public Task<bool> TryAddAsync(
        WorkflowExecution execution,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();

        if (execution.Revision != 0)
        {
            throw new ArgumentException(
                "Cannot add an execution with a non-zero revision.",
                nameof(execution));
        }

        var snapshot = RehydrateSnapshot(execution);

        return Task.FromResult(_store.TryAdd(execution.Id, snapshot));
    }

    public Task<WorkflowExecution?> GetAsync(
        WorkflowExecutionId id,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(id));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _store.TryGetValue(id, out var stored);

        return Task.FromResult(stored is null ? null : RehydrateSnapshot(stored));
    }

    public Task<WorkflowExecution?> TryUpdateAsync(
        WorkflowExecution execution,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(execution);
        cancellationToken.ThrowIfCancellationRequested();

        if (expectedRevision < 0)
        {
            throw new ArgumentException(
                "Expected revision cannot be negative.",
                nameof(expectedRevision));
        }

        if (!_store.TryGetValue(execution.Id, out var stored))
        {
            return Task.FromResult<WorkflowExecution?>(null);
        }

        if (stored.Revision != expectedRevision)
        {
            return Task.FromResult<WorkflowExecution?>(null);
        }

        ValidateImmutableFields(stored, execution);

        if (IsNoOp(stored, execution))
        {
            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(stored));
        }

        ValidateLifecycleTransition(stored.Status, execution.Status);

        var updated = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            execution.Status,
            expectedRevision + 1,
            execution.CreatedAt,
            execution.StartedAt,
            execution.CompletedAt);

        if (_store.TryUpdate(execution.Id, updated, stored))
        {
            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(updated));
        }

        return Task.FromResult<WorkflowExecution?>(null);
    }


    private static WorkflowExecution RehydrateSnapshot(WorkflowExecution execution)
    {
        return WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            execution.Status,
            execution.Revision,
            execution.CreatedAt,
            execution.StartedAt,
            execution.CompletedAt);
    }

    private static bool IsNoOp(WorkflowExecution stored, WorkflowExecution submitted)
    {
        return stored.Status == submitted.Status
            && stored.StartedAt == submitted.StartedAt
            && stored.CompletedAt == submitted.CompletedAt;
    }

    private static void ValidateImmutableFields(
        WorkflowExecution stored,
        WorkflowExecution submitted)
    {
        if (submitted.AssetId != stored.AssetId)
        {
            throw new ArgumentException(
                "AssetId cannot be changed through update.",
                "execution");
        }

        if (submitted.WorkflowDefinitionId != stored.WorkflowDefinitionId)
        {
            throw new ArgumentException(
                "WorkflowDefinitionId cannot be changed through update.",
                "execution");
        }

        if (submitted.WorkflowDefinitionVersion != stored.WorkflowDefinitionVersion)
        {
            throw new ArgumentException(
                "WorkflowDefinitionVersion cannot be changed through update.",
                "execution");
        }

        if (submitted.CreatedAt != stored.CreatedAt)
        {
            throw new ArgumentException(
                "CreatedAt cannot be changed through update.",
                "execution");
        }
    }

    private static void ValidateLifecycleTransition(
        WorkflowExecutionStatus stored,
        WorkflowExecutionStatus submitted)
    {
        var valid = (stored, submitted) switch
        {
            (WorkflowExecutionStatus.Created, WorkflowExecutionStatus.Running) => true,
            (WorkflowExecutionStatus.Running, WorkflowExecutionStatus.Completed) => true,
            (WorkflowExecutionStatus.Running, WorkflowExecutionStatus.Failed) => true,
            (WorkflowExecutionStatus.Running, WorkflowExecutionStatus.Cancelled) => true,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException(
                $"Invalid lifecycle transition from {stored} to {submitted}.",
                "execution");
        }
    }
}
