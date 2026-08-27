namespace Lunar.Api.Contracts;

public sealed class CreateAssetResponse
{
    public Guid AssetId { get; init; }

    public required string Name { get; init; }

    public required string AssetType { get; init; }
}
