using System.Collections.Concurrent;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Infrastructure.Persistence;

public sealed class InMemoryGenerationInputRecordRepository : IGenerationInputRecordRepository
{
    private readonly ConcurrentDictionary<WorkflowExecutionId, GenerationInputRecord> _store = new();

    public Task<bool> TryAddAsync(
        GenerationInputRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_store.TryAdd(record.WorkflowExecutionId, record));
    }

    public Task<IReadOnlyList<GenerationInputRecord>> GetByAssetIdAsync(
        AssetId assetId,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var matching = _store.Values
            .Where(record => record.AssetId == assetId)
            .ToList();

        return Task.FromResult<IReadOnlyList<GenerationInputRecord>>(matching.AsReadOnly());
    }
}
