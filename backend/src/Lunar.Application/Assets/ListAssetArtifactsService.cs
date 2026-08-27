using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;

namespace Lunar.Application.Assets;

public sealed class ListAssetArtifactsService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IArtifactRepository _artifactRepository;

    public ListAssetArtifactsService(
        IAssetRepository assetRepository,
        IArtifactRepository artifactRepository)
    {
        ArgumentNullException.ThrowIfNull(assetRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);

        _assetRepository = assetRepository;
        _artifactRepository = artifactRepository;
    }


    public async Task<Result<IReadOnlyList<Artifact>>> ListAsync(
        AssetId assetId,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        var asset = await _assetRepository.GetAsync(assetId, cancellationToken);

        if (asset is null)
        {
            return Result<IReadOnlyList<Artifact>>.Failure(
                new AssetNotFound(assetId));
        }

        var artifacts = await _artifactRepository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        var ordered = artifacts
            .OrderByDescending(artifact => artifact.CreatedAt)
            .ThenByDescending(artifact => artifact.Id.Value)
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<Artifact>>.Success(ordered);
    }
}
