using Lunar.Core.Assets;

namespace Lunar.Core.Artifacts;

public interface IArtifactRepository
{
    Task<bool> TryAddAsync(
        Artifact artifact,
        CancellationToken cancellationToken = default);

    Task<Artifact?> GetAsync(
        ArtifactId id,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
        AssetId assetId,
        CancellationToken cancellationToken = default);
}
