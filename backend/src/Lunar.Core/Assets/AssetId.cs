namespace Lunar.Core.Assets;

public readonly record struct AssetId(Guid Value)
{
    public static AssetId New()
    {
        return new(Guid.CreateVersion7());
    }
}