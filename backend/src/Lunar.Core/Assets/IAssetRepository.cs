namespace Lunar.Core.Assets;

public interface IAssetRepository
{
    Task<bool> TryAddAsync(
        Asset asset,
        CancellationToken cancellationToken = default);

    Task<Asset?> GetAsync(
        AssetId id,
        CancellationToken cancellationToken = default);
}
