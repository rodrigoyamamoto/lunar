namespace Lunar.Api.Contracts;

public sealed class CreateAssetRequest
{
    public required string Name { get; init; }

    public required string AssetType { get; init; }
}
