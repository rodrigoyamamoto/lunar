using System.Collections.Concurrent;
using Lunar.Core.Artifacts;

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
}
