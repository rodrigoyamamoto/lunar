using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Integration;

public class DurableArtifactContentRoundTripTests : IDisposable
{
    private readonly string _rootPath;

    private static readonly AssetId SharedAssetId = AssetId.New();

    private static readonly ArtifactContent SharedContent =
        new BinaryArtifactContent(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 },
            "image/jpeg");


    public DurableArtifactContentRoundTripTests()
    {
        _rootPath = Path.Combine(
            System.IO.Path.GetTempPath(),
            "lunar-integration-" + Guid.NewGuid().ToString("N"));
    }


    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }


    [Fact]
    public async Task ExecuteAsync_WithRealLocalStore_RoundTripRetrievesExactContent()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var contentStore = new LocalFileArtifactContentStore(_rootPath);
        var definitionId = WorkflowDefinitionId.New();

        var definition = new WorkflowDefinition(
            definitionId,
            1,
            "Integration Test Definition",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        await definitionRepository.TryAddAsync(definition);

        var execution = WorkflowExecution.Create(
            SharedAssetId,
            definitionId,
            1);

        await executionRepository.TryAddAsync(execution);
        execution.Start();
        await executionRepository.TryUpdateAsync(execution, 0);

        var executor = new StubExecutor(SharedContent);

        var service = new ExecuteWorkflowStepService(
            executionRepository,
            definitionRepository,
            artifactRepository,
            executor,
            contentStore);

        var result = await service.ExecuteAsync(execution.Id, 1, new TextPromptInput("test prompt"));

        Assert.True(result.IsSuccess);
        var artifactId = result.Value!.Artifact.Id;

        // A brand-new store instance pointed at the same root must retrieve
        // the exact content persisted by the service.
        var newStore = new LocalFileArtifactContentStore(_rootPath);
        var retrieved = await newStore.GetAsync(artifactId);

        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(
            ((BinaryArtifactContent)SharedContent).Data.ToArray(),
            binary.Data.ToArray());
        Assert.Equal("image/jpeg", binary.MediaType);
    }


    [Fact]
    public async Task ExecuteAsync_MetadataRejectsWithRealStore_CompensationRemovesContent()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new RejectingArtifactRepository();
        var contentStore = new LocalFileArtifactContentStore(_rootPath);
        var definitionId = WorkflowDefinitionId.New();

        var definition = new WorkflowDefinition(
            definitionId,
            1,
            "Integration Test Definition",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        await definitionRepository.TryAddAsync(definition);

        var execution = WorkflowExecution.Create(
            SharedAssetId,
            definitionId,
            1);

        await executionRepository.TryAddAsync(execution);
        execution.Start();
        await executionRepository.TryUpdateAsync(execution, 0);

        var executor = new StubExecutor(SharedContent);

        var service = new ExecuteWorkflowStepService(
            executionRepository,
            definitionRepository,
            artifactRepository,
            executor,
            contentStore);

        var result = await service.ExecuteAsync(execution.Id, 1, new TextPromptInput("test prompt"));

        Assert.True(result.IsFailure);
        Assert.IsType<ArtifactPersistenceFailed>(result.Error);

        // The rejecting repository captures the artifact ID it was asked to persist.
        var rejectedArtifactId = artifactRepository.CapturedArtifact!.Id;

        // A new store instance must confirm content was removed by compensation.
        var newStore = new LocalFileArtifactContentStore(_rootPath);
        var retrieved = await newStore.GetAsync(rejectedArtifactId);

        Assert.Null(retrieved);
    }


    [Fact]
    public async Task ExecuteAsync_MetadataThrowsWithRealStore_CompensationRemovesContent()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new ThrowingArtifactRepository(
            new InvalidOperationException("Metadata persistence failed."));
        var contentStore = new LocalFileArtifactContentStore(_rootPath);
        var definitionId = WorkflowDefinitionId.New();

        var definition = new WorkflowDefinition(
            definitionId,
            1,
            "Integration Test Definition",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        await definitionRepository.TryAddAsync(definition);

        var execution = WorkflowExecution.Create(
            SharedAssetId,
            definitionId,
            1);

        await executionRepository.TryAddAsync(execution);
        execution.Start();
        await executionRepository.TryUpdateAsync(execution, 0);

        var executor = new StubExecutor(SharedContent);

        var service = new ExecuteWorkflowStepService(
            executionRepository,
            definitionRepository,
            artifactRepository,
            executor,
            contentStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(execution.Id, 1, new TextPromptInput("test prompt")));

        var thrownArtifactId = artifactRepository.CapturedArtifact!.Id;

        var newStore = new LocalFileArtifactContentStore(_rootPath);
        var retrieved = await newStore.GetAsync(thrownArtifactId);

        Assert.Null(retrieved);
    }


    private sealed class StubExecutor : ICapabilityExecutor
    {
        private readonly ArtifactContent _content;

        public StubExecutor(ArtifactContent content)
        {
            _content = content;
        }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(new CapabilityExecutionOutput(
                    "test-output.jpg",
                    ArtifactType.ConceptImage,
                    Array.Empty<ArtifactId>(),
                    _content)));
        }
    }


    private sealed class RejectingArtifactRepository : IArtifactRepository
    {
        public Artifact? CapturedArtifact { get; private set; }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            cancellationToken.ThrowIfCancellationRequested();

            CapturedArtifact = artifact;

            return Task.FromResult(false);
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Artifact?>(null);
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Artifact>>(Array.Empty<Artifact>());
        }
    }


    private sealed class ThrowingArtifactRepository : IArtifactRepository
    {
        private readonly Exception _exception;

        public Artifact? CapturedArtifact { get; private set; }


        public ThrowingArtifactRepository(Exception exception)
        {
            _exception = exception;
        }


        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            CapturedArtifact = artifact;

            throw _exception;
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Artifact>>(Array.Empty<Artifact>());
        }
    }
}
