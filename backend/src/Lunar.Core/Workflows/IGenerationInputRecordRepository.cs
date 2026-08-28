using Lunar.Core.Assets;

namespace Lunar.Core.Workflows;

public interface IGenerationInputRecordRepository
{
    Task<bool> TryAddAsync(
        GenerationInputRecord record,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenerationInputRecord>> GetByAssetIdAsync(
        AssetId assetId,
        CancellationToken cancellationToken = default);
}
