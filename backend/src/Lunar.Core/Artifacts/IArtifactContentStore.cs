namespace Lunar.Core.Artifacts;

public interface IArtifactContentStore
{
    Task<bool> TryAddAsync(
        ArtifactId artifactId,
        ArtifactContent content,
        CancellationToken cancellationToken = default);

    Task<ArtifactContent?> GetAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken = default);

    Task<bool> TryDeleteAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken = default);
}
