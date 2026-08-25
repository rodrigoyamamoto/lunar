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


    [Fact]
    public void ValidConstruction_ShouldPreserveIdentityNameTypeAndInitialStatus()
    {
        var id = AssetId.New();

        var asset = new Asset(
            id,
            "Ancient Gate",
            AssetType.Environment);


        Assert.Equal(id, asset.Id);
        Assert.Equal("Ancient Gate", asset.Name);
        Assert.Equal(AssetType.Environment, asset.Type);
        Assert.Equal(AssetStatus.Draft, asset.Status);
    }


    [Fact]
    public void EmptyIdentifier_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Asset(
                new AssetId(Guid.Empty),
                "Corrupted Knight",
                AssetType.Character));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankName_ShouldBeRejected(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Asset(
                AssetId.New(),
                name!,
                AssetType.Character));
    }


    [Fact]
    public void Name_ShouldBePreservedExactly()
    {
        const string name = "Ancient  Gate";

        var asset = new Asset(
            AssetId.New(),
            name,
            AssetType.Environment);


        Assert.Equal(name, asset.Name);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructAllFields()
    {
        var id = AssetId.New();
        var createdAt = DateTimeOffset.UtcNow;

        var asset = Asset.Rehydrate(
            id,
            "Test Character",
            AssetType.Character,
            AssetStatus.Completed,
            createdAt);

        Assert.Equal(id, asset.Id);
        Assert.Equal("Test Character", asset.Name);
        Assert.Equal(AssetType.Character, asset.Type);
        Assert.Equal(AssetStatus.Completed, asset.Status);
        Assert.Equal(createdAt, asset.CreatedAt);
    }


    [Fact]
    public void Rehydrate_ShouldRejectEmptyId()
    {
        Assert.Throws<ArgumentException>(() =>
            Asset.Rehydrate(
                new AssetId(Guid.Empty),
                "Test Character",
                AssetType.Character,
                AssetStatus.Draft,
                DateTimeOffset.UtcNow));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rehydrate_ShouldRejectBlankName(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            Asset.Rehydrate(
                AssetId.New(),
                name!,
                AssetType.Character,
                AssetStatus.Draft,
                DateTimeOffset.UtcNow));
    }
}