using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Artifacts;

public class RecordWorkflowArtifactServiceTests
{
    private static AssetId SharedAssetId { get; } = AssetId.New();


    private static WorkflowExecution CreateCreatedExecution(AssetId? assetId = null)
    {
        return WorkflowExecution.Create(
            assetId ?? SharedAssetId,
            WorkflowDefinitionId.New(),
            1);
    }


    private static async Task<WorkflowExecution> PersistCreatedExecutionAsync(
        IWorkflowExecutionRepository repository,
        AssetId? assetId = null)
    {
        var execution = CreateCreatedExecution(assetId);
        await repository.TryAddAsync(execution);
        return await repository.GetAsync(execution.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistRunningExecutionAsync(
        IWorkflowExecutionRepository repository,
        AssetId? assetId = null)
    {
        var execution = await PersistCreatedExecutionAsync(repository, assetId);
        execution.Start();
        await repository.TryUpdateAsync(execution, 0);
        return await repository.GetAsync(execution.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistTerminalExecutionAsync(
        IWorkflowExecutionRepository repository,
        WorkflowExecutionStatus terminalStatus,
        AssetId? assetId = null)
    {
        var running = await PersistRunningExecutionAsync(repository, assetId);
        var transitioned = WorkflowExecution.Rehydrate(
            running.Id,
            running.AssetId,
            running.WorkflowDefinitionId,
            running.WorkflowDefinitionVersion,
            terminalStatus,
            running.Revision,
            running.CreatedAt,
            running.StartedAt,
            DateTimeOffset.UtcNow);

        await repository.TryUpdateAsync(transitioned, running.Revision);
        return await repository.GetAsync(running.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static Artifact CreateWorkflowArtifact(
        WorkflowExecutionId executionId,
        AssetId? assetId = null,
        string name = "corrupted-knight-concept.png",
        ArtifactType type = ArtifactType.ConceptImage,
        IEnumerable<ArtifactId>? sourceArtifactIds = null)
    {
        return new Artifact(
            ArtifactId.New(),
            assetId ?? SharedAssetId,
            name,
            type,
            sourceArtifactIds ?? Array.Empty<ArtifactId>(),
            executionId);
    }


    private static RecordWorkflowArtifactService CreateService(
        IWorkflowExecutionRepository? executionRepository = null,
        IArtifactRepository? artifactRepository = null)
    {
        return new RecordWorkflowArtifactService(
            executionRepository ?? new InMemoryWorkflowExecutionRepository(),
            artifactRepository ?? new InMemoryArtifactRepository());
    }


    [Fact]
    public async Task RecordAsync_RunningExecutionWithMatchingArtifact_ShouldReturnSuccess()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var service = CreateService(executionRepository, artifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(artifact.Id, result.Value!.Id);
    }


    [Fact]
    public async Task RecordAsync_Success_ShouldPersistArtifact()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var artifact = new Artifact(
            ArtifactId.New(),
            execution.AssetId,
            "Ancient  Gate Texture",
            ArtifactType.Texture,
            new[] { sourceA, sourceB },
            execution.Id);

        var service = CreateService(executionRepository, artifactRepository);

        await service.RecordAsync(execution.Id, artifact);

        var retrieved = await artifactRepository.GetAsync(artifact.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(artifact.Id, retrieved!.Id);
        Assert.Equal(artifact.AssetId, retrieved.AssetId);
        Assert.Equal("Ancient  Gate Texture", retrieved.Name);
        Assert.Equal(ArtifactType.Texture, retrieved.Type);
        Assert.Equal(execution.Id, retrieved.SourceExecutionId);
        Assert.Equal(2, retrieved.SourceArtifactIds.Count);
        Assert.Equal(sourceA, retrieved.SourceArtifactIds[0]);
        Assert.Equal(sourceB, retrieved.SourceArtifactIds[1]);
        Assert.Equal(artifact.CreatedAt, retrieved.CreatedAt);
    }


    [Fact]
    public async Task RecordAsync_MissingExecution_ShouldReturnNotFoundWithExactId()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var service = CreateService(executionRepository, trackingArtifactRepository);

        var executionId = WorkflowExecutionId.New();
        var artifact = CreateWorkflowArtifact(executionId);

        var result = await service.RecordAsync(executionId, artifact);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionNotFound>(result.Error);
        Assert.Equal(executionId, error.WorkflowExecutionId);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when the execution is missing.");
    }


    [Fact]
    public async Task RecordAsync_CreatedExecution_ShouldReturnNotRunning()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistCreatedExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowExecutionNotRunning>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Created, error.CurrentStatus);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when the execution is not Running.");
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public async Task RecordAsync_TerminalExecution_ShouldReturnNotRunning(
        WorkflowExecutionStatus terminalStatus)
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistTerminalExecutionAsync(
            executionRepository,
            terminalStatus);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowExecutionNotRunning>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(terminalStatus, error.CurrentStatus);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when the execution is terminal.");
    }


    [Fact]
    public async Task RecordAsync_ArtifactWithoutSourceExecutionId_ShouldReturnProvenanceMissing()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);

        var artifact = new Artifact(
            ArtifactId.New(),
            execution.AssetId,
            "imported-reference.png",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            sourceExecutionId: null);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactWorkflowProvenanceMissing>(result.Error);
        Assert.Equal(artifact.Id, error.ArtifactId);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when provenance is missing.");
    }


    [Fact]
    public async Task RecordAsync_SourceExecutionIdMismatch_ShouldReturnExecutionMismatch()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var otherExecutionId = WorkflowExecutionId.New();

        var artifact = CreateWorkflowArtifact(otherExecutionId, execution.AssetId);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactWorkflowExecutionMismatch>(result.Error);
        Assert.Equal(artifact.Id, error.ArtifactId);
        Assert.Equal(execution.Id, error.RequestedWorkflowExecutionId);
        Assert.Equal(otherExecutionId, error.ArtifactSourceExecutionId);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called on provenance mismatch.");
    }


    [Fact]
    public async Task RecordAsync_AssetIdMismatch_ShouldReturnAssetMismatch()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var otherAssetId = AssetId.New();

        var artifact = CreateWorkflowArtifact(execution.Id, otherAssetId);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactWorkflowAssetMismatch>(result.Error);
        Assert.Equal(artifact.Id, error.ArtifactId);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(execution.AssetId, error.ExecutionAssetId);
        Assert.Equal(otherAssetId, error.ArtifactAssetId);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called on asset mismatch.");
    }


    [Fact]
    public async Task RecordAsync_CrossAssetSourceArtifactIds_ShouldSucceed()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var crossAssetSource = ArtifactId.New();

        var artifact = CreateWorkflowArtifact(
            execution.Id,
            execution.AssetId,
            sourceArtifactIds: new[] { crossAssetSource });

        var service = CreateService(executionRepository, artifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value!.SourceArtifactIds);
        Assert.Equal(crossAssetSource, result.Value.SourceArtifactIds[0]);
    }


    [Fact]
    public async Task RecordAsync_DuplicateArtifactId_ShouldReturnPersistenceFailed()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var service = CreateService(executionRepository, artifactRepository);

        await service.RecordAsync(execution.Id, artifact);
        var secondResult = await service.RecordAsync(execution.Id, artifact);

        Assert.True(secondResult.IsFailure);
        var error = Assert.IsType<ArtifactPersistenceFailed>(secondResult.Error);
        Assert.Equal(artifact.Id, error.ArtifactId);
    }


    [Fact]
    public async Task RecordAsync_DuplicateArtifactId_ShouldNotOverwriteOriginal()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var id = ArtifactId.New();

        var original = new Artifact(
            id,
            execution.AssetId,
            "Original Concept",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            execution.Id);

        var duplicate = new Artifact(
            id,
            execution.AssetId,
            "Replacement Model",
            ArtifactType.Model,
            Array.Empty<ArtifactId>(),
            execution.Id);

        var service = CreateService(executionRepository, artifactRepository);

        await service.RecordAsync(execution.Id, original);
        await service.RecordAsync(execution.Id, duplicate);

        var retrieved = await artifactRepository.GetAsync(id);

        Assert.NotNull(retrieved);
        Assert.Equal("Original Concept", retrieved!.Name);
        Assert.Equal(ArtifactType.ConceptImage, retrieved.Type);
    }


    [Fact]
    public async Task RecordAsync_NullArtifact_ShouldThrow()
    {
        var trackingExecutionRepository = new TrackingWorkflowExecutionRepository();
        var service = CreateService(trackingExecutionRepository);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.RecordAsync(WorkflowExecutionId.New(), null!));

        Assert.False(
            trackingExecutionRepository.GetAsyncWasCalled,
            "GetAsync must not be called when the artifact is null.");
    }


    [Fact]
    public async Task RecordAsync_EmptyExecutionId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.RecordAsync(
                new WorkflowExecutionId(Guid.Empty),
                CreateWorkflowArtifact(WorkflowExecutionId.New())));
    }


    [Fact]
    public async Task RecordAsync_PreCancelledToken_ShouldThrowAndNotCallTryAdd()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var service = CreateService(executionRepository, trackingArtifactRepository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RecordAsync(execution.Id, artifact, cts.Token));

        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when the token is pre-cancelled.");
    }


    [Fact]
    public async Task RecordAsync_TokenCancelledAfterExecutionLookup_ShouldThrowAtArtifactBoundary()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        using var cts = new CancellationTokenSource();
        var cancellingArtifactRepository =
            new CancelBeforeAddArtifactRepository(cts);
        var service = CreateService(executionRepository, cancellingArtifactRepository);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RecordAsync(execution.Id, artifact, cts.Token));

        Assert.True(
            cancellingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must have been reached after execution lookup succeeded.");
    }


    [Fact]
    public async Task RecordAsync_Success_ShouldNotMutateExecution()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var artifact = CreateWorkflowArtifact(execution.Id);

        var originalStatus = execution.Status;
        var originalRevision = execution.Revision;
        var originalStartedAt = execution.StartedAt;
        var originalCompletedAt = execution.CompletedAt;

        var service = CreateService(executionRepository, artifactRepository);

        await service.RecordAsync(execution.Id, artifact);

        var reloaded = await executionRepository.GetAsync(execution.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(originalStatus, reloaded!.Status);
        Assert.Equal(originalRevision, reloaded.Revision);
        Assert.Equal(originalStartedAt, reloaded.StartedAt);
        Assert.Equal(originalCompletedAt, reloaded.CompletedAt);
    }


    [Fact]
    public async Task RecordAsync_Success_ShouldPreserveProvenance()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();

        var artifact = CreateWorkflowArtifact(
            execution.Id,
            execution.AssetId,
            sourceArtifactIds: new[] { sourceA, sourceB });

        var service = CreateService(executionRepository, artifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsSuccess);
        Assert.Equal(execution.Id, result.Value!.SourceExecutionId);
        Assert.Equal(2, result.Value.SourceArtifactIds.Count);
        Assert.Equal(sourceA, result.Value.SourceArtifactIds[0]);
        Assert.Equal(sourceB, result.Value.SourceArtifactIds[1]);
    }


    [Fact]
    public async Task RecordAsync_Success_ShouldPreserveNameExactly()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var execution = await PersistRunningExecutionAsync(executionRepository);

        const string name = "Ancient  Gate Texture";

        var artifact = CreateWorkflowArtifact(
            execution.Id,
            execution.AssetId,
            name: name);

        var service = CreateService(executionRepository, artifactRepository);

        var result = await service.RecordAsync(execution.Id, artifact);

        Assert.True(result.IsSuccess);
        Assert.Equal(name, result.Value!.Name);
    }


    [Fact]
    public void Constructor_NullExecutionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordWorkflowArtifactService(null!, new InMemoryArtifactRepository()));
    }


    [Fact]
    public void Constructor_NullArtifactRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RecordWorkflowArtifactService(new InMemoryWorkflowExecutionRepository(), null!));
    }


    private sealed class TrackingArtifactRepository : IArtifactRepository
    {
        public bool TryAddAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            cancellationToken.ThrowIfCancellationRequested();

            TryAddAsyncWasCalled = true;

            return Task.FromResult(true);
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


    private sealed class TrackingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        public bool GetAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }

        public Task<WorkflowExecution?> GetAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Workflow execution identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            GetAsyncWasCalled = true;

            return Task.FromResult<WorkflowExecution?>(null);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            if (expectedRevision < 0)
            {
                throw new ArgumentException(
                    "Expected revision cannot be negative.",
                    nameof(expectedRevision));
            }

            return Task.FromResult<WorkflowExecution?>(null);
        }
    }


    private sealed class CancelBeforeAddArtifactRepository : IArtifactRepository
    {
        private readonly CancellationTokenSource _cts;

        public CancelBeforeAddArtifactRepository(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public bool TryAddAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);

            TryAddAsyncWasCalled = true;

            _cts.Cancel();

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
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
}
