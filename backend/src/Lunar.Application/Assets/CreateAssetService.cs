using Lunar.Application.Errors;
using Lunar.Core.Assets;

namespace Lunar.Application.Assets;

public sealed class CreateAssetService
{
    private readonly IAssetRepository _assetRepository;

    public CreateAssetService(IAssetRepository assetRepository)
    {
        ArgumentNullException.ThrowIfNull(assetRepository);
        _assetRepository = assetRepository;
    }


    public async Task<Result<Asset>> CreateAsync(
        string name,
        AssetType assetType,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Asset name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        var assetId = AssetId.New();
        var asset = new Asset(assetId, name, assetType);

        var persisted = await _assetRepository.TryAddAsync(asset, cancellationToken);

        if (!persisted)
        {
            return Result<Asset>.Failure(new AssetPersistenceFailed(assetId));
        }

        return Result<Asset>.Success(asset);
    }
}
