using Lunar.Application.Assets;
using Lunar.Application.Errors;
using Lunar.Core.Assets;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Assets;

public class CreateAssetServiceTests
{
    [Fact]
    public void Constructor_NullRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new CreateAssetService(null!));
    }


    [Fact]
    public async Task CreateAsync_NullName_ShouldThrow()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(null!, AssetType.Character));
    }


    [Fact]
    public async Task CreateAsync_EmptyName_ShouldThrow()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("", AssetType.Character));
    }


    [Fact]
    public async Task CreateAsync_WhitespaceName_ShouldThrow()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync("   ", AssetType.Character));
    }


    [Fact]
    public async Task CreateAsync_Success_ReturnsNonEmptyAssetId()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        var result = await service.CreateAsync("Test Asset", AssetType.Environment);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.Id.Value);
    }


    [Fact]
    public async Task CreateAsync_Success_PreservesExactName()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());
        var name = "  Ruined Gothic Watchtower  ";

        var result = await service.CreateAsync(name, AssetType.Environment);

        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.Value!.Name);
    }


    [Fact]
    public async Task CreateAsync_Success_PreservesExactAssetType()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        var result = await service.CreateAsync("Test", AssetType.Weapon);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetType.Weapon, result.Value!.Type);
    }


    [Fact]
    public async Task CreateAsync_Success_AssetWasPersisted()
    {
        var repository = new InMemoryAssetRepository();
        var service = new CreateAssetService(repository);

        var result = await service.CreateAsync("Test Asset", AssetType.Character);

        Assert.True(result.IsSuccess);
        var persisted = await repository.GetAsync(result.Value!.Id);
        Assert.NotNull(persisted);
        Assert.Equal(result.Value.Id, persisted!.Id);
        Assert.Equal(result.Value.Name, persisted.Name);
        Assert.Equal(result.Value.Type, persisted.Type);
    }


    [Fact]
    public async Task CreateAsync_Success_StatusIsDraft()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());

        var result = await service.CreateAsync("Test Asset", AssetType.Character);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssetStatus.Draft, result.Value!.Status);
    }


    [Fact]
    public async Task CreateAsync_RepositoryReturnsFalse_ReturnsAssetPersistenceFailed()
    {
        var repository = new RejectingAssetRepository();
        var service = new CreateAssetService(repository);

        var result = await service.CreateAsync("Test Asset", AssetType.Character);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<AssetPersistenceFailed>(result.Error);
        Assert.NotEqual(Guid.Empty, error.AssetId.Value);
    }


    [Fact]
    public async Task CreateAsync_RepositoryThrows_PropagatesException()
    {
        var repository = new ThrowingAssetRepository();
        var service = new CreateAssetService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync("Test Asset", AssetType.Character));
    }


    [Fact]
    public async Task CreateAsync_PreCancelledToken_Propagates()
    {
        var service = new CreateAssetService(new InMemoryAssetRepository());
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CreateAsync("Test Asset", AssetType.Character, cts.Token));
    }


    [Fact]
    public async Task CreateAsync_Success_RepositoryAddCalledOnce()
    {
        var repository = new TrackingAssetRepository();
        var service = new CreateAssetService(repository);

        await service.CreateAsync("Test Asset", AssetType.Character);

        Assert.Equal(1, repository.TryAddCallCount);
    }


    private sealed class RejectingAssetRepository : IAssetRepository
    {
        public Task<bool> TryAddAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<Asset?> GetAsync(AssetId id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Asset?>(null);
        }
    }


    private sealed class ThrowingAssetRepository : IAssetRepository
    {
        public Task<bool> TryAddAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is unavailable.");
        }

        public Task<Asset?> GetAsync(AssetId id, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is unavailable.");
        }
    }


    private sealed class TrackingAssetRepository : IAssetRepository
    {
        private readonly InMemoryAssetRepository _inner = new();
        public int TryAddCallCount { get; private set; }

        public Task<bool> TryAddAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            TryAddCallCount++;
            return _inner.TryAddAsync(asset, cancellationToken);
        }

        public Task<Asset?> GetAsync(AssetId id, CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(id, cancellationToken);
        }
    }
}
