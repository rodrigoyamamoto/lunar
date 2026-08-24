using Lunar.Core.Primitives;

namespace Lunar.Core.Assets;

public readonly record struct AssetId(Guid Value)
{
    public static AssetId New()
    {
        return new AssetId(IdGenerator.New());
    }
}