using Lunar.Core.Assets;

namespace Lunar.Tests.Unit.Assets;

public class AssetLifecycleTests
{
    [Fact]
    public void MarkAsProcessing_ShouldChangeStatus()
    {
        var asset = CreateAsset();


        asset.MarkAsProcessing();


        Assert.Equal(
            AssetStatus.Processing,
            asset.Status);
    }


    [Fact]
    public void MarkAsCompleted_ShouldChangeStatus()
    {
        var asset = CreateAsset();


        asset.MarkAsCompleted();


        Assert.Equal(
            AssetStatus.Completed,
            asset.Status);
    }


    [Fact]
    public void MarkAsFailed_ShouldChangeStatus()
    {
        var asset = CreateAsset();


        asset.MarkAsFailed();


        Assert.Equal(
            AssetStatus.Failed,
            asset.Status);
    }


    private static Asset CreateAsset()
    {
        return new Asset(
            AssetId.New(),
            "Test Asset",
            AssetType.Character);
    }
}