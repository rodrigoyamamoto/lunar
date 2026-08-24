using System.Collections.Concurrent;
using Lunar.Core.Workflows;

namespace Lunar.Infrastructure.Persistence;

public sealed class InMemoryWorkflowDefinitionRepository : IWorkflowDefinitionRepository
{
    private readonly ConcurrentDictionary<DefinitionVersionKey, WorkflowDefinition> _store = new();

    public Task<bool> TryAddAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(definition);
        cancellationToken.ThrowIfCancellationRequested();

        var key = new DefinitionVersionKey(definition.Id, definition.Version);

        return Task.FromResult(_store.TryAdd(key, definition));
    }

    public Task<WorkflowDefinition?> GetAsync(
        WorkflowDefinitionId id,
        int version,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow definition identifier cannot be empty.",
                nameof(id));
        }

        if (version < 1)
        {
            throw new ArgumentException(
                "Workflow definition version must be a positive integer.",
                nameof(version));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var key = new DefinitionVersionKey(id, version);

        _store.TryGetValue(key, out var definition);

        return Task.FromResult(definition);
    }


    private readonly record struct DefinitionVersionKey(
        WorkflowDefinitionId Id,
        int Version);
}
