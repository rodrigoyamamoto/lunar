using System.Collections.Concurrent;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;

namespace Lunar.Infrastructure.Persistence;

public sealed class InMemoryArtifactRepository : IArtifactRepository
{
    private readonly ConcurrentDictionary<ArtifactId, Artifact> _store = new();

    public Task<bool> TryAddAsync(
        Artifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_store.TryAdd(artifact.Id, artifact));
    }

    public Task<Artifact?> GetAsync(
        ArtifactId id,
        CancellationToken cancellationToken = default)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(id));
        }

        cancellationToken.ThrowIfCancellationRequested();

        _store.TryGetValue(id, out var stored);

        return Task.FromResult(stored);
    }

    public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
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
            .Where(artifact => artifact.AssetId == assetId)
            .ToList();

        return Task.FromResult<IReadOnlyList<Artifact>>(matching.AsReadOnly());
    }
}
