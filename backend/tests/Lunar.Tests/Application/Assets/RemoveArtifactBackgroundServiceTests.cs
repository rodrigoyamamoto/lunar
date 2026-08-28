using Lunar.Application.Assets;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Application.Assets;

public class RemoveArtifactBackgroundServiceTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] PngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    private static readonly BinaryArtifactContent JpegContent = new(JpegBytes, "image/jpeg");
    private static readonly BinaryArtifactContent PngContent = new(PngBytes, "image/png");


    [Fact]
    public async Task RemoveBackgroundAsync_SourceArtifactMissing_ReturnsArtifactNotFound()
    {
        var service = CreateService();

        var result = await service.RemoveBackgroundAsync(ArtifactId.New());

        Assert.True(result.IsFailure);
        Assert.IsType<ArtifactNotFound>(result.Error);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_SourceContentMissing_ReturnsArtifactContentNotFound()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var sourceArtifact = CreateSourceArtifact(artifactRepo);
        await artifactRepo.TryAddAsync(sourceArtifact);

        var service = CreateService(artifactRepo: artifactRepo, contentStore: contentStore);

        var result = await service.RemoveBackgroundAsync(sourceArtifact.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<ArtifactContentNotFound>(result.Error);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_UnsupportedMediaType_ReturnsUnsupportedArtifactContent()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var sourceArtifact = CreateSourceArtifact(artifactRepo);
        await artifactRepo.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id,
            new BinaryArtifactContent(new byte[] { 0x00, 0x01 }, "application/octet-stream"));

        var service = CreateService(artifactRepo: artifactRepo, contentStore: contentStore);

        var result = await service.RemoveBackgroundAsync(sourceArtifact.Id);

        Assert.True(result.IsFailure);
        Assert.IsType<UnsupportedArtifactContent>(result.Error);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_Success_ReturnsDerivedArtifactWithLineage()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var assetRepo = new InMemoryAssetRepository();
        var definitionRepo = new InMemoryWorkflowDefinitionRepository();
        var executionRepo = new InMemoryWorkflowExecutionRepository();
        var inputRecordRepo = new InMemoryGenerationInputRecordRepository();

        var assetId = AssetId.New();
        await assetRepo.TryAddAsync(new Asset(assetId, "Test", AssetType.Weapon));

        // Use a non-ConceptImage source type to prove the derived Artifact
        // preserves the source type rather than hard-coding ConceptImage.
        var sourceArtifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "Knight Sprite",
            ArtifactType.Texture,
            Array.Empty<ArtifactId>());
        await artifactRepo.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, JpegContent);

        var executor = new TransformExecutor(PngContent);
        var service = CreateService(
            artifactRepo: artifactRepo,
            contentStore: contentStore,
            assetRepo: assetRepo,
            definitionRepo: definitionRepo,
            executionRepo: executionRepo,
            inputRecordRepo: inputRecordRepo,
            executor: executor);

        var result = await service.RemoveBackgroundAsync(sourceArtifact.Id);

        Assert.True(result.IsSuccess);
        var derived = result.Value!;
        Assert.Equal(assetId, derived.ProducedArtifact.Artifact.AssetId);
        Assert.Equal(ArtifactType.Texture, derived.ProducedArtifact.Artifact.Type);
        Assert.Equal("Knight Sprite - background removed", derived.ProducedArtifact.Artifact.Name);
        Assert.Single(derived.ProducedArtifact.Artifact.SourceArtifactIds);
        Assert.Equal(sourceArtifact.Id, derived.ProducedArtifact.Artifact.SourceArtifactIds[0]);
        Assert.Equal(derived.WorkflowExecutionId, derived.ProducedArtifact.Artifact.SourceExecutionId);

        var persistedContent = await contentStore.GetAsync(derived.ProducedArtifact.Artifact.Id);
        Assert.NotNull(persistedContent);
        Assert.IsType<BinaryArtifactContent>(persistedContent);
        Assert.Equal("image/png", ((BinaryArtifactContent)persistedContent!).MediaType);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_Success_SourceArtifactUnchanged()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var assetRepo = new InMemoryAssetRepository();
        var definitionRepo = new InMemoryWorkflowDefinitionRepository();
        var executionRepo = new InMemoryWorkflowExecutionRepository();
        var inputRecordRepo = new InMemoryGenerationInputRecordRepository();

        var assetId = AssetId.New();
        await assetRepo.TryAddAsync(new Asset(assetId, "Test", AssetType.Weapon));

        var sourceArtifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "Sword",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
        await artifactRepo.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, JpegContent);

        var executor = new TransformExecutor(PngContent);
        var service = CreateService(
            artifactRepo: artifactRepo,
            contentStore: contentStore,
            assetRepo: assetRepo,
            definitionRepo: definitionRepo,
            executionRepo: executionRepo,
            inputRecordRepo: inputRecordRepo,
            executor: executor);

        await service.RemoveBackgroundAsync(sourceArtifact.Id);

        var sourceAfter = await artifactRepo.GetAsync(sourceArtifact.Id);
        Assert.NotNull(sourceAfter);
        Assert.Empty(sourceAfter!.SourceArtifactIds);
        Assert.Equal("Sword", sourceAfter.Name);

        var sourceContent = await contentStore.GetAsync(sourceArtifact.Id);
        Assert.NotNull(sourceContent);
        Assert.Equal("image/jpeg", ((BinaryArtifactContent)sourceContent!).MediaType);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_ProviderFailure_ReturnsFailureNoDerivedArtifact()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var assetRepo = new InMemoryAssetRepository();
        var definitionRepo = new InMemoryWorkflowDefinitionRepository();
        var executionRepo = new InMemoryWorkflowExecutionRepository();
        var inputRecordRepo = new InMemoryGenerationInputRecordRepository();

        var assetId = AssetId.New();
        await assetRepo.TryAddAsync(new Asset(assetId, "Test", AssetType.Weapon));

        var sourceArtifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "Sword",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
        await artifactRepo.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, JpegContent);

        var executor = new FailingExecutor();
        var service = CreateService(
            artifactRepo: artifactRepo,
            contentStore: contentStore,
            assetRepo: assetRepo,
            definitionRepo: definitionRepo,
            executionRepo: executionRepo,
            inputRecordRepo: inputRecordRepo,
            executor: executor);

        var result = await service.RemoveBackgroundAsync(sourceArtifact.Id);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_DoesNotCreateGenerationInputRecord()
    {
        var artifactRepo = new InMemoryArtifactRepository();
        var contentStore = new InMemoryArtifactContentStore();
        var assetRepo = new InMemoryAssetRepository();
        var definitionRepo = new InMemoryWorkflowDefinitionRepository();
        var executionRepo = new InMemoryWorkflowExecutionRepository();
        var inputRecordRepo = new InMemoryGenerationInputRecordRepository();

        var assetId = AssetId.New();
        await assetRepo.TryAddAsync(new Asset(assetId, "Test", AssetType.Weapon));

        var sourceArtifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "Sword",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
        await artifactRepo.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, JpegContent);

        var executor = new TransformExecutor(PngContent);
        var service = CreateService(
            artifactRepo: artifactRepo,
            contentStore: contentStore,
            assetRepo: assetRepo,
            definitionRepo: definitionRepo,
            executionRepo: executionRepo,
            inputRecordRepo: inputRecordRepo,
            executor: executor);

        var result = await service.RemoveBackgroundAsync(sourceArtifact.Id);

        Assert.True(result.IsSuccess);
        var records = await inputRecordRepo.GetByAssetIdAsync(assetId);
        Assert.Empty(records);
    }

    [Fact]
    public async Task RemoveBackgroundAsync_EmptyArtifactId_Throws()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RemoveBackgroundAsync(new ArtifactId(Guid.Empty)));
    }


    private static Artifact CreateSourceArtifact(InMemoryArtifactRepository repo)
    {
        return new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "Test",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
    }

    private static RemoveArtifactBackgroundService CreateService(
        InMemoryArtifactRepository? artifactRepo = null,
        InMemoryArtifactContentStore? contentStore = null,
        InMemoryAssetRepository? assetRepo = null,
        InMemoryWorkflowDefinitionRepository? definitionRepo = null,
        InMemoryWorkflowExecutionRepository? executionRepo = null,
        InMemoryGenerationInputRecordRepository? inputRecordRepo = null,
        ICapabilityExecutor? executor = null)
    {
        artifactRepo ??= new InMemoryArtifactRepository();
        contentStore ??= new InMemoryArtifactContentStore();
        assetRepo ??= new InMemoryAssetRepository();
        definitionRepo ??= new InMemoryWorkflowDefinitionRepository();
        executionRepo ??= new InMemoryWorkflowExecutionRepository();
        inputRecordRepo ??= new InMemoryGenerationInputRecordRepository();
        executor ??= new TransformExecutor(PngContent);

        var target = new ForegroundIsolationWorkflowTarget(
            WorkflowDefinitionId.New(), 1, 1);

        var definition = new WorkflowDefinition(
            target.WorkflowDefinitionId,
            target.Version,
            "Foreground Isolation",
            new[] { new WorkflowStep(target.StepPosition, CapabilityId.New()) });
        definitionRepo.TryAddAsync(definition).GetAwaiter().GetResult();

        var generateService = new GenerateArtifactService(
            definitionRepo,
            new CreateWorkflowExecutionService(
                assetRepo, definitionRepo, executionRepo,
                NullLogger<CreateWorkflowExecutionService>.Instance),
            new StartWorkflowExecutionService(
                executionRepo,
                NullLogger<StartWorkflowExecutionService>.Instance),
            new ExecuteWorkflowStepService(
                executionRepo, definitionRepo, artifactRepo,
                new SingleCapabilityExecutorResolver(executor),
                contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            inputRecordRepo,
            NullLogger<GenerateArtifactService>.Instance);

        return new RemoveArtifactBackgroundService(
            artifactRepo,
            contentStore,
            generateService,
            target,
            NullLogger<RemoveArtifactBackgroundService>.Instance);
    }


    private sealed class TransformExecutor : ICapabilityExecutor
    {
        private readonly BinaryArtifactContent _outputContent;

        public TransformExecutor(BinaryArtifactContent outputContent)
        {
            _outputContent = outputContent;
        }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request.Input is not ImageArtifactInput)
            {
                throw new ArgumentException("Expected ImageArtifactInput");
            }

            var output = new CapabilityExecutionOutput(
            _outputContent);

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }
    }

    private sealed class FailingExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionFailed(
                    new CapabilityExecutionFailure(
                        CapabilityExecutionFailureKind.TemporarilyUnavailable)));
        }
    }

    private sealed class InMemoryArtifactContentStore : IArtifactContentStore
    {
        private readonly Dictionary<ArtifactId, ArtifactContent> _store = new();

        public Task<bool> TryAddAsync(ArtifactId artifactId, ArtifactContent content, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryAdd(artifactId, content));
        }

        public Task<ArtifactContent?> GetAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryGetValue(artifactId, out var content) ? content : null);
        }

        public Task<bool> TryDeleteAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Remove(artifactId));
        }
    }
}
