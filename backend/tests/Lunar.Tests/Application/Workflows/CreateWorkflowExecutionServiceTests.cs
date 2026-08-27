using Lunar.Application;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Application.Workflows;

public class CreateWorkflowExecutionServiceTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();


    [Fact]
    public void Constructor_NullAssetRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CreateWorkflowExecutionService(
                null!,
                new InMemoryWorkflowDefinitionRepository(),
                new InMemoryWorkflowExecutionRepository(),
                NullLogger<CreateWorkflowExecutionService>.Instance));
    }

    [Fact]
    public void Constructor_NullWorkflowDefinitionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CreateWorkflowExecutionService(
                new InMemoryAssetRepository(),
                null!,
                new InMemoryWorkflowExecutionRepository(),
                NullLogger<CreateWorkflowExecutionService>.Instance));
    }

    [Fact]
    public void Constructor_NullWorkflowExecutionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CreateWorkflowExecutionService(
                new InMemoryAssetRepository(),
                new InMemoryWorkflowDefinitionRepository(),
                null!,
                NullLogger<CreateWorkflowExecutionService>.Instance));
    }

    [Fact]
    public async Task CreateAsync_EmptyAssetId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                new AssetId(Guid.Empty),
                SharedDefinitionId,
                1));
    }

    [Fact]
    public async Task CreateAsync_EmptyWorkflowDefinitionId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                SharedAssetId,
                new WorkflowDefinitionId(Guid.Empty),
                1));
    }

    [Fact]
    public async Task CreateAsync_VersionLessThanOne_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateAsync(
                SharedAssetId,
                SharedDefinitionId,
                0));
    }

    [Fact]
    public async Task CreateAsync_MissingAsset_ShouldReturnAssetNotFound()
    {
        var service = CreateService();

        var result = await service.CreateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1);

        Assert.True(result.IsFailure);
        Assert.IsType<AssetNotFound>(result.Error);
    }

    [Fact]
    public async Task CreateAsync_MissingWorkflowDefinition_ShouldReturnWorkflowDefinitionNotFound()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var service = CreateService(assetRepository: assetRepository);

        var result = await service.CreateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1);

        Assert.True(result.IsFailure);
        Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
    }

    [Fact]
    public async Task CreateAsync_RepositoryInsertionFalse_ShouldReturnWorkflowExecutionPersistenceFailed()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executionRepository = new RejectingWorkflowExecutionRepository();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        var result = await service.CreateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1);

        Assert.True(result.IsFailure);
        Assert.IsType<WorkflowExecutionPersistenceFailed>(result.Error);
    }

    [Fact]
    public async Task CreateAsync_RepositoryTechnicalException_ShouldPropagate()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executionRepository = new ThrowingWorkflowExecutionRepository(
            new InvalidOperationException("Database connection lost."));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(SharedAssetId, SharedDefinitionId, 1));
    }

    [Fact]
    public async Task CreateAsync_PreCancelledToken_ShouldPropagate()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.CreateAsync(SharedAssetId, SharedDefinitionId, 1, cts.Token));
    }

    [Fact]
    public async Task CreateAsync_Success_ShouldReturnExecutionWithExactIds()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var result = await service.CreateAsync(SharedAssetId, SharedDefinitionId, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(SharedAssetId, result.Value!.AssetId);
        Assert.Equal(SharedDefinitionId, result.Value!.WorkflowDefinitionId);
        Assert.Equal(1, result.Value!.WorkflowDefinitionVersion);
    }

    [Fact]
    public async Task CreateAsync_Success_ShouldBeginInCreatedStatus()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var result = await service.CreateAsync(SharedAssetId, SharedDefinitionId, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowExecutionStatus.Created, result.Value!.Status);
        Assert.Equal(0, result.Value!.Revision);
    }

    [Fact]
    public async Task CreateAsync_Success_ShouldNotStartExecution()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var result = await service.CreateAsync(SharedAssetId, SharedDefinitionId, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowExecutionStatus.Created, result.Value!.Status);
        Assert.Null(result.Value!.StartedAt);
    }

    [Fact]
    public async Task CreateAsync_Success_PersistedInstanceMatchesResult()
    {
        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test Asset", AssetType.Character));

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executionRepository = new InMemoryWorkflowExecutionRepository();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        var result = await service.CreateAsync(SharedAssetId, SharedDefinitionId, 1);

        Assert.True(result.IsSuccess);

        var persisted = await executionRepository.GetAsync(result.Value!.Id);
        Assert.NotNull(persisted);
        Assert.Equal(result.Value!.Id, persisted!.Id);
        Assert.Equal(result.Value!.AssetId, persisted.AssetId);
        Assert.Equal(result.Value!.WorkflowDefinitionId, persisted.WorkflowDefinitionId);
        Assert.Equal(result.Value!.WorkflowDefinitionVersion, persisted.WorkflowDefinitionVersion);
        Assert.Equal(result.Value!.Status, persisted.Status);
    }


    private static CreateWorkflowExecutionService CreateService(
        IAssetRepository? assetRepository = null,
        IWorkflowDefinitionRepository? definitionRepository = null,
        IWorkflowExecutionRepository? executionRepository = null)
    {
        return new CreateWorkflowExecutionService(
            assetRepository ?? new InMemoryAssetRepository(),
            definitionRepository ?? new InMemoryWorkflowDefinitionRepository(),
            executionRepository ?? new InMemoryWorkflowExecutionRepository(),
            NullLogger<CreateWorkflowExecutionService>.Instance);
    }

    private static WorkflowDefinition CreateDefinition(WorkflowDefinitionId id, int version)
    {
        return new WorkflowDefinition(
            id,
            version,
            "Test Definition",
            new[] { new WorkflowStep(1, CapabilityId.New()) });
    }


    private sealed class RejectingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(false);
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

            return Task.FromResult<WorkflowExecution?>(null);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WorkflowExecution?>(null);
        }
    }


    private sealed class ThrowingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        private readonly Exception _exception;

        public ThrowingWorkflowExecutionRepository(Exception exception)
        {
            _exception = exception;
        }

        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<WorkflowExecution?> GetAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
