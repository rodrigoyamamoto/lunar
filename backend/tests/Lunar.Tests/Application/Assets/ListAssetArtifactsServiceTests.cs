using Lunar.Application.Assets;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Application.Assets;

public class ListAssetArtifactsServiceTests
{
    [Fact]
    public void Constructor_NullAssetRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ListAssetArtifactsService(null!, new InMemoryArtifactRepository(), new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance));
    }


    [Fact]
    public void Constructor_NullArtifactRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ListAssetArtifactsService(new InMemoryAssetRepository(), null!, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance));
    }


    [Fact]
    public void Constructor_NullGenerationInputRecordRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ListAssetArtifactsService(new InMemoryAssetRepository(), new InMemoryArtifactRepository(), null!, NullLogger<ListAssetArtifactsService>.Instance));
    }


    [Fact]
    public async Task ListAsync_EmptyAssetId_ShouldThrow()
    {
        var service = new ListAssetArtifactsService(
            new InMemoryAssetRepository(),
            new InMemoryArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ListAsync(new AssetId(Guid.Empty)));
    }


    [Fact]
    public async Task ListAsync_MissingAsset_ReturnsAssetNotFound()
    {
        var service = new ListAssetArtifactsService(
            new InMemoryAssetRepository(),
            new InMemoryArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(AssetId.New());

        Assert.True(result.IsFailure);
        Assert.IsType<AssetNotFound>(result.Error);
    }


    [Fact]
    public async Task ListAsync_MissingAsset_DoesNotQueryArtifactRepository()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new TrackingArtifactRepository();
        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

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
            new InMemoryArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

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

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetA);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.All(result.Value!, a => Assert.Equal(assetA, a.Artifact.AssetId));
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

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);
        Assert.Equal("New", result.Value[0].Artifact.Name);
        Assert.Equal("Old", result.Value[1].Artifact.Name);
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
        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        for (var i = 1; i < result.Value!.Count; i++)
        {
            var prev = result.Value[i - 1].Artifact;
            var curr = result.Value[i].Artifact;
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
            new InMemoryArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

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
            new InMemoryArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

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
            new ThrowingArtifactRepository(),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<ListAssetArtifactsService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ListAsync(assetId));
    }


    [Fact]
    public async Task ListAsync_ArtifactWithMatchingGenerationInput_AssociatesExactPrompt()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var executionId = WorkflowExecutionId.New();
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetId, name: "A1", sourceExecutionId: executionId));

        var prompt = new TextPromptInput("a ruined obsidian sword with  internal  whitespace");
        await inputRepository.TryAddAsync(
            new GenerationInputRecord(executionId, assetId, prompt));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, inputRepository, NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        var item = result.Value![0];
        Assert.NotNull(item.GenerationInput);
        Assert.Equal(executionId, item.GenerationInput!.WorkflowExecutionId);
        Assert.Equal(assetId, item.GenerationInput!.AssetId);
        Assert.Equal("a ruined obsidian sword with  internal  whitespace", item.GenerationInput!.Prompt.Prompt);
    }


    [Fact]
    public async Task ListAsync_ArtifactWithNullSourceExecutionId_HasNullGenerationInput()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetId, name: "A1", sourceExecutionId: null));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Null(result.Value![0].GenerationInput);
    }


    [Fact]
    public async Task ListAsync_ArtifactWithSourceExecutionIdButMissingRecord_HasNullGenerationInput()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var executionId = WorkflowExecutionId.New();
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetId, name: "A1", sourceExecutionId: executionId));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, new InMemoryGenerationInputRecordRepository(), NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Null(result.Value![0].GenerationInput);
    }


    [Fact]
    public async Task ListAsync_MultipleArtifacts_MapToTheirOwnPrompts()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var executionA = WorkflowExecutionId.New();
        var executionB = WorkflowExecutionId.New();
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetId, name: "A", sourceExecutionId: executionA));
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetId, name: "B", sourceExecutionId: executionB));

        await inputRepository.TryAddAsync(
            new GenerationInputRecord(executionA, assetId, new TextPromptInput("prompt A")));
        await inputRepository.TryAddAsync(
            new GenerationInputRecord(executionB, assetId, new TextPromptInput("prompt B")));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, inputRepository, NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetId);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Count);

        var itemA = result.Value!.Single(i => i.Artifact.Name == "A");
        var itemB = result.Value!.Single(i => i.Artifact.Name == "B");
        Assert.Equal("prompt A", itemA.GenerationInput!.Prompt.Prompt);
        Assert.Equal("prompt B", itemB.GenerationInput!.Prompt.Prompt);
    }


    [Fact]
    public async Task ListAsync_InputRecordForAnotherAsset_NeverAttached()
    {
        var assetRepository = new InMemoryAssetRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var assetA = AssetId.New();
        var assetB = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetA, "A", AssetType.Character));
        await assetRepository.TryAddAsync(new Asset(assetB, "B", AssetType.Weapon));

        var executionA = WorkflowExecutionId.New();
        var executionB = WorkflowExecutionId.New();
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetA, name: "A1", sourceExecutionId: executionA));
        await artifactRepository.TryAddAsync(
            CreateArtifact(assetId: assetB, name: "B1", sourceExecutionId: executionB));

        await inputRepository.TryAddAsync(
            new GenerationInputRecord(executionB, assetB, new TextPromptInput("prompt for B")));

        var service = new ListAssetArtifactsService(assetRepository, artifactRepository, inputRepository, NullLogger<ListAssetArtifactsService>.Instance);

        var result = await service.ListAsync(assetA);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!);
        Assert.Null(result.Value![0].GenerationInput);
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
