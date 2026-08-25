using System.Collections.Concurrent;
using Lunar.Core.Assets;

namespace Lunar.Infrastructure.Persistence;

public sealed class InMemoryAssetRepository : IAssetRepository
{
    private readonly ConcurrentDictionary<AssetId, Asset> _store = new();

    public Task<bool> TryAddAsync(
        Asset asset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(asset);
        cancellationToken.ThrowIfCancellationRequested();

        var snapshot = RehydrateSnapshot(asset);

        return Task.FromResult(_store.TryAdd(asset.Id, snapshot));
    }

    public Task<Asset?> GetAsync(
        AssetId id,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(id));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _store.TryGetValue(id, out var stored);

        return Task.FromResult(stored is null ? null : RehydrateSnapshot(stored));
    }


    private static Asset RehydrateSnapshot(Asset asset)
    {
        return Asset.Rehydrate(
            asset.Id,
            asset.Name,
            asset.Type,
            asset.Status,
            asset.CreatedAt);
    }
}
