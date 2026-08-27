using Lunar.Application.Assets;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Assets;

public class ListAssetArtifactsServiceTests
{
    [Fact]
    public void Constructor_NullAssetRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ListAssetArtifactsService(null!, new InMemoryArtifactRepository()));
    }


    [Fact]
    public void Constructor_NullArtifactRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ListAssetArtifactsService(new InMemoryAssetRepository(), null!));
    }


    [Fact]
    public async Task ListAsync_EmptyAssetId_ShouldThrow()
    {
        var service = new ListAssetArtifactsService(
            new InMemoryAssetRepository(),
            new InMemoryArtifactRepository());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListAsync(new AssetId(Guid.Empty)));
    }


    [Fact]
    public async Task ListAsync_MissingAsset_ReturnsAssetNotFound()
    {
        var service = new ListAssetArtifactsService(
            new InMemoryAssetRepository(),
            new InMemoryArtifactRepository());

        var result = await service.ListAsync(AssetId.New());

        Assert.True(result.IsFailure);
        Assert.IsType<AssetNotFound>(result.Error);
    }


    [Fact]
    public async Task ListAsync_MissingAsset_DoesNotQueryArtifactRepository()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new TrackingArtifactRepository();
        var service = new ListAssetArtifactsService(assetRepository, artifactRepository);

        await service.ListAsync(AssetId.New());

        Assert.Equal(0, artifactRepository.GetByAssetIdCallCount);
    }


    [Fact]
    public async Task ListAsync_ExistingAssetNoArtifacts_ReturnsSuccessEmpty()
    {
        var assetRepository = new InMemoryAssetRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var service = new ListAssetArtifactsService(
            assetRepository,
            new InMemoryArtifactRepository());

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value!);
    }


    [Fact]
    public async Task ListAsync_ExistingAssetWithArtifacts_ReturnsOnlyExactAssetArtifacts()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var assetA = AssetId.New();
        var assetB = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetA, "A", AssetType.Character));
        await assetRepository.TryAddAsync(new Asset(assetB, "B", AssetType.Weapon));
        await artifactRepository.TryAddAsync(CreateArtifact(assetId: assetA, name: "A1"));
        await artifactRepository.TryAddAsync(CreateArtifact(assetId: assetB, name: "B1"));
        await artifactRepository.TryAddAsync(CreateArtifact(assetId: assetA, name: "A2"));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository);

        var result = await service.ListAsync(assetA);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value!, a => Assert.Equal(assetA, a.AssetId));
    }


    [Fact]
    public async Task ListAsync_NewestFirstOrdering()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var oldArtifact = CreateArtifact(assetId: assetId, name: "Old");
        await artifactRepository.TryAddAsync(oldArtifact);

        await Task.Delay(20);

        var newArtifact = CreateArtifact(assetId: assetId, name: "New");
        await artifactRepository.TryAddAsync(newArtifact);

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("New", result.Value[0].Name);
        Assert.Equal("Old", result.Value[1].Name);
    }


    [Fact]
    public async Task ListAsync_EqualCreatedAt_DeterministicTieBreak()
    {
        var assetId = AssetId.New();
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var lowerId = new ArtifactId(new Guid("01000000-0000-7ff7-8000-000000000000"));
        var higherId = new ArtifactId(new Guid("02000000-0000-7ff7-8000-000000000000"));
        var lowerArtifact = CreateArtifact(lowerId, assetId, "Lower");
        var higherArtifact = CreateArtifact(higherId, assetId, "Higher");

        var artifactRepository = new FixedReturnArtifactRepository(
            new[] { lowerArtifact, higherArtifact });
        var service = new ListAssetArtifactsService(assetRepository, artifactRepository);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        for (var i = 1; i < result.Value!.Count; i++)
        {
            var prev = result.Value[i - 1];
            var curr = result.Value[i];
            var prevKey = (prev.CreatedAt, prev.Id.Value);
            var currKey = (curr.CreatedAt, curr.Id.Value);
            Assert.True(prevKey.CompareTo(currKey) >= 0,
                $"Result must be sorted by CreatedAt descending then Id descending. " +
                $"Index {i - 1} key {prevKey} < index {i} key {currKey}.");
        }
    }


    [Fact]
    public async Task ListAsync_PreCancelledToken_Propagates()
    {
        var assetRepository = new InMemoryAssetRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var service = new ListAssetArtifactsService(
            assetRepository,
            new InMemoryArtifactRepository());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ListAsync(assetId, cts.Token));
    }


    [Fact]
    public async Task ListAsync_AssetRepositoryThrows_Propagates()
    {
        var service = new ListAssetArtifactsService(
            new ThrowingAssetRepository(),
            new InMemoryArtifactRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListAsync(AssetId.New()));
    }


    [Fact]
    public async Task ListAsync_ArtifactRepositoryThrows_Propagates()
    {
        var assetRepository = new InMemoryAssetRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var service = new ListAssetArtifactsService(
            assetRepository,
            new ThrowingArtifactRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListAsync(assetId));
    }


    private static Artifact CreateArtifact(
        ArtifactId? id = null,
        AssetId? assetId = null,
        string name = "test-output.jpg",
        ArtifactType type = ArtifactType.ConceptImage,
        IEnumerable<ArtifactId>? sourceArtifactIds = null,
        WorkflowExecutionId? sourceExecutionId = null)
    {
        return new Artifact(
            id ?? ArtifactId.New(),
            assetId ?? AssetId.New(),
            name,
            type,
            sourceArtifactIds ?? Array.Empty<ArtifactId>(),
            sourceExecutionId);
    }


    private sealed class TrackingArtifactRepository : IArtifactRepository
    {
        private readonly InMemoryArtifactRepository _inner = new();
        public int GetByAssetIdCallCount { get; private set; }

        public Task<bool> TryAddAsync(Artifact artifact, CancellationToken cancellationToken = default)
        {
            return _inner.TryAddAsync(artifact, cancellationToken);
        }

        public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(id, cancellationToken);
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            GetByAssetIdCallCount++;
            return _inner.GetByAssetIdAsync(assetId, cancellationToken);
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


    private sealed class ThrowingArtifactRepository : IArtifactRepository
    {
        public Task<bool> TryAddAsync(Artifact artifact, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is unavailable.");
        }

        public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is unavailable.");
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Repository is unavailable.");
        }
    }


    private sealed class FixedReturnArtifactRepository : IArtifactRepository
    {
        private readonly IReadOnlyList<Artifact> _artifacts;

        public FixedReturnArtifactRepository(IReadOnlyList<Artifact> artifacts)
        {
            _artifacts = artifacts;
        }

        public Task<bool> TryAddAsync(Artifact artifact, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<Artifact?> GetAsync(ArtifactId id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Artifact?>(null);
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_artifacts);
        }
    }
}
