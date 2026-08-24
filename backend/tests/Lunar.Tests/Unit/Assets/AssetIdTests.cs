using Lunar.Core.Assets;

namespace Lunar.Tests.Unit.Assets;

public class AssetIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var assetId = AssetId.New();

        Assert.NotEqual(
            Guid.Empty,
            assetId.Value);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = AssetId.New();
        var second = AssetId.New();

        Assert.NotEqual(
            first,
            second);
    }
}