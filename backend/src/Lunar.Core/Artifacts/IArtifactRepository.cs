namespace Lunar.Core.Artifacts;

public interface IArtifactRepository
{
    Task<bool> TryAddAsync(
        Artifact artifact,
        CancellationToken cancellationToken = default);

    Task<Artifact?> GetAsync(
        ArtifactId id,
        CancellationToken cancellationToken = default);
}
