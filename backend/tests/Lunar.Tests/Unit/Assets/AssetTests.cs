using Lunar.Core.Assets;

namespace Lunar.Tests.Unit.Assets;

public class AssetTests
{
    [Fact]
    public void NewAsset_ShouldStartAsDraft()
    {
        var asset = new Asset(
            AssetId.New(),
            "Corrupted Knight",
            AssetType.Character);


        Assert.Equal(
            AssetStatus.Draft,
            asset.Status);
    }


    [Fact]
    public void NewAsset_ShouldKeepProvidedInformation()
    {
        var asset = new Asset(
            AssetId.New(),
            "Dark Sword",
            AssetType.Weapon);


        Assert.Equal(
            "Dark Sword",
            asset.Name);


        Assert.Equal(
            AssetType.Weapon,
            asset.Type);
    }
}