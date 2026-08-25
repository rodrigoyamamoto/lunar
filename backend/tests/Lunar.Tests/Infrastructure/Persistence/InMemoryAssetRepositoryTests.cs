using Lunar.Core.Assets;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Infrastructure.Persistence;

public class InMemoryAssetRepositoryTests
{
    private static Asset CreateAsset(
        AssetId id,
        string name = "Warrior Character",
        AssetType type = AssetType.Character)
    {
        return new Asset(id, name, type);
    }


    [Fact]
    public async Task TryAddAsync_FirstInsertion_ShouldSucceed()
    {
        var repository = new InMemoryAssetRepository();
        var asset = CreateAsset(AssetId.New());

        var result = await repository.TryAddAsync(asset);

        Assert.True(result);
    }


    [Fact]
    public async Task TryAddAsync_DuplicateId_ShouldNotOverwrite()
    {
        var repository = new InMemoryAssetRepository();
        var id = AssetId.New();
        var original = CreateAsset(id, "Original Name");
        var duplicate = CreateAsset(id, "Replacement Name");

        await repository.TryAddAsync(original);
        var secondResult = await repository.TryAddAsync(duplicate);

        Assert.False(secondResult);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal("Original Name", retrieved!.Name);
    }


    [Fact]
    public async Task GetAsync_StoredAsset_CanBeRetrievedByExactId()
    {
        var repository = new InMemoryAssetRepository();
        var id = AssetId.New();
        var asset = CreateAsset(id, "Test Character", AssetType.Weapon);

        await repository.TryAddAsync(asset);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
        Assert.Equal("Test Character", retrieved.Name);
        Assert.Equal(AssetType.Weapon, retrieved.Type);
        Assert.Equal(AssetStatus.Draft, retrieved.Status);
    }


    [Fact]
    public async Task GetAsync_MissingId_ShouldReturnNull()
    {
        var repository = new InMemoryAssetRepository();

        var result = await repository.GetAsync(AssetId.New());

        Assert.Null(result);
    }


    [Fact]
    public async Task GetAsync_EmptyId_ShouldThrow()
    {
        var repository = new InMemoryAssetRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(new AssetId(Guid.Empty)));
    }


    [Fact]
    public async Task TryAddAsync_NullAsset_ShouldThrow()
    {
        var repository = new InMemoryAssetRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryAddAsync(null!));
    }


    [Fact]
    public async Task GetAsync_ShouldReturnIsolatedSnapshot()
    {
        var repository = new InMemoryAssetRepository();
        var id = AssetId.New();
        var asset = CreateAsset(id);

        await repository.TryAddAsync(asset);

        var retrieved = await repository.GetAsync(id);
        retrieved!.MarkAsProcessing();

        var retrievedAgain = await repository.GetAsync(id);

        Assert.NotNull(retrievedAgain);
        Assert.Equal(AssetStatus.Draft, retrievedAgain!.Status);
    }


    [Fact]
    public async Task TryAddAsync_ShouldStoreIsolatedSnapshot()
    {
        var repository = new InMemoryAssetRepository();
        var id = AssetId.New();
        var asset = CreateAsset(id);

        await repository.TryAddAsync(asset);
        asset.MarkAsProcessing();

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(AssetStatus.Draft, retrieved!.Status);
    }


    [Fact]
    public async Task GetAsync_PreCancelledToken_ShouldThrow()
    {
        var repository = new InMemoryAssetRepository();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAsync(AssetId.New(), cts.Token));
    }


    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldThrow()
    {
        var repository = new InMemoryAssetRepository();
        var asset = CreateAsset(AssetId.New());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(asset, cts.Token));
    }
}
