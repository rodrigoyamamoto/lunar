using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Infrastructure.Persistence;

public class InMemoryArtifactRepositoryTests
{
    private static Artifact CreateArtifact(
        ArtifactId? id = null,
        AssetId? assetId = null,
        string name = "corrupted-knight-concept.png",
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


    [Fact]
    public async Task TryAddAsync_FirstInsertion_ShouldSucceed()
    {
        var repository = new InMemoryArtifactRepository();
        var artifact = CreateArtifact();

        var result = await repository.TryAddAsync(artifact);

        Assert.True(result);
    }


    [Fact]
    public async Task TryAddAsync_DuplicateId_ShouldReturnFalse()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var original = CreateArtifact(id, name: "Original Concept");
        var duplicate = CreateArtifact(id, name: "Replacement Concept");

        await repository.TryAddAsync(original);
        var secondResult = await repository.TryAddAsync(duplicate);

        Assert.False(secondResult);
    }


    [Fact]
    public async Task TryAddAsync_DuplicateId_ShouldNotOverwrite()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var original = CreateArtifact(id, name: "Original Concept", type: ArtifactType.ConceptImage);
        var duplicate = CreateArtifact(id, name: "Replacement Model", type: ArtifactType.Model);

        await repository.TryAddAsync(original);
        await repository.TryAddAsync(duplicate);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal("Original Concept", retrieved!.Name);
        Assert.Equal(ArtifactType.ConceptImage, retrieved.Type);
    }


    [Fact]
    public async Task GetAsync_StoredArtifact_ShouldReturnAllFieldsExactly()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var assetId = AssetId.New();
        var executionId = WorkflowExecutionId.New();
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var sourceC = ArtifactId.New();

        var artifact = new Artifact(
            id,
            assetId,
            "Ancient  Gate Texture",
            ArtifactType.Texture,
            new[] { sourceA, sourceB, sourceC },
            executionId);

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
        Assert.Equal(assetId, retrieved.AssetId);
        Assert.Equal("Ancient  Gate Texture", retrieved.Name);
        Assert.Equal(ArtifactType.Texture, retrieved.Type);
        Assert.Equal(executionId, retrieved.SourceExecutionId);
        Assert.Equal(3, retrieved.SourceArtifactIds.Count);
        Assert.Equal(sourceA, retrieved.SourceArtifactIds[0]);
        Assert.Equal(sourceB, retrieved.SourceArtifactIds[1]);
        Assert.Equal(sourceC, retrieved.SourceArtifactIds[2]);
        Assert.Equal(artifact.CreatedAt, retrieved.CreatedAt);
    }


    [Fact]
    public async Task GetAsync_MissingId_ShouldReturnNull()
    {
        var repository = new InMemoryArtifactRepository();

        var result = await repository.GetAsync(ArtifactId.New());

        Assert.Null(result);
    }


    [Fact]
    public async Task GetAsync_EmptyId_ShouldThrow()
    {
        var repository = new InMemoryArtifactRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(new ArtifactId(Guid.Empty)));
    }


    [Fact]
    public async Task TryAddAsync_NullArtifact_ShouldThrow()
    {
        var repository = new InMemoryArtifactRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryAddAsync(null!));
    }


    [Fact]
    public async Task GetAsync_PreCancelledToken_ShouldThrow()
    {
        var repository = new InMemoryArtifactRepository();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAsync(ArtifactId.New(), cts.Token));
    }


    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldThrow()
    {
        var repository = new InMemoryArtifactRepository();
        var artifact = CreateArtifact();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(artifact, cts.Token));
    }


    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldNotPersist()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var artifact = CreateArtifact(id);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(artifact, cts.Token));

        var retrieved = await repository.GetAsync(id);

        Assert.Null(retrieved);
    }


    [Fact]
    public async Task GetAsync_AfterSuccessfulInsert_ShouldReturnSameArtifact()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var artifact = CreateArtifact(id, name: "Test Artifact");

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
        Assert.Equal("Test Artifact", retrieved.Name);
    }


    [Fact]
    public async Task TryAddAsync_WithSourceExecutionId_ShouldPreserveProvenance()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var executionId = WorkflowExecutionId.New();

        var artifact = CreateArtifact(
            id,
            sourceExecutionId: executionId,
            name: "generated-concept.png");

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(executionId, retrieved!.SourceExecutionId);
    }


    [Fact]
    public async Task TryAddAsync_WithNullSourceExecutionId_ShouldPreserveNull()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();

        var artifact = CreateArtifact(id, sourceExecutionId: null);

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Null(retrieved!.SourceExecutionId);
    }


    [Fact]
    public async Task TryAddAsync_WithSourceArtifactIds_ShouldPreserveLineage()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var sourceC = ArtifactId.New();

        var artifact = CreateArtifact(
            id,
            sourceArtifactIds: new[] { sourceA, sourceB, sourceC });

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved!.SourceArtifactIds.Count);
        Assert.Equal(sourceA, retrieved.SourceArtifactIds[0]);
        Assert.Equal(sourceB, retrieved.SourceArtifactIds[1]);
        Assert.Equal(sourceC, retrieved.SourceArtifactIds[2]);
    }


    [Fact]
    public async Task TryAddAsync_WithEmptySourceArtifactIds_ShouldPreserveEmptyLineage()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();

        var artifact = CreateArtifact(id, sourceArtifactIds: Array.Empty<ArtifactId>());

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Empty(retrieved!.SourceArtifactIds);
    }


    [Fact]
    public async Task TryAddAsync_WithCrossAssetLineage_ShouldNotBeRejected()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var assetId = AssetId.New();
        var crossAssetSource = ArtifactId.New();

        var artifact = new Artifact(
            id,
            assetId,
            "cross-asset-derivative.png",
            ArtifactType.ConceptImage,
            new[] { crossAssetSource });

        var result = await repository.TryAddAsync(artifact);

        Assert.True(result);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Single(retrieved!.SourceArtifactIds);
        Assert.Equal(crossAssetSource, retrieved.SourceArtifactIds[0]);
    }


    [Fact]
    public async Task GetAsync_ShouldReturnImmutableArtifact()
    {
        var repository = new InMemoryArtifactRepository();
        var id = ArtifactId.New();
        var artifact = CreateArtifact(id);

        await repository.TryAddAsync(artifact);

        var retrieved = await repository.GetAsync(id);

        Assert.NotNull(retrieved);

        var exposed = retrieved!.SourceArtifactIds;

        Assert.IsNotType<List<ArtifactId>>(exposed);

        var act = () => ((ICollection<ArtifactId>)exposed).Add(ArtifactId.New());

        Assert.Throws<NotSupportedException>(act);
    }
}
