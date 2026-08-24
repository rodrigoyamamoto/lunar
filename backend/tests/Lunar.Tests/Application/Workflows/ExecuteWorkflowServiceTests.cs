using Lunar.Application;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Workflows;

public class ExecuteWorkflowServiceTests
{
    private static WorkflowDefinition CreateDefinition(
        WorkflowDefinitionId id,
        int version)
    {
        return new WorkflowDefinition(
            id,
            version,
            "Character Generation",
            new[] { new WorkflowStep(1, CapabilityId.New()) });
    }


    private static ExecuteWorkflowService CreateService(
        IWorkflowDefinitionRepository? definitionRepository = null,
        IWorkflowExecutionRepository? executionRepository = null)
    {
        return new ExecuteWorkflowService(
            definitionRepository ?? new InMemoryWorkflowDefinitionRepository(),
            executionRepository ?? new InMemoryWorkflowExecutionRepository());
    }


    [Fact]
    public async Task ExecuteAsync_ValidInput_ShouldReturnSuccessWithExecution()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();

        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var assetId = AssetId.New();
        var service = CreateService(definitionRepository, executionRepository);

        var result = await service.ExecuteAsync(assetId, definitionId, 1);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(assetId, result.Value!.AssetId);
        Assert.Equal(definitionId, result.Value.WorkflowDefinitionId);
        Assert.Equal(1, result.Value.WorkflowDefinitionVersion);
        Assert.Equal(WorkflowExecutionStatus.Created, result.Value.Status);
        Assert.Equal(0, result.Value.Revision);
    }


    [Fact]
    public async Task ExecuteAsync_ValidInput_ShouldPersistExecutionToRepository()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();

        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var service = CreateService(definitionRepository, executionRepository);

        var result = await service.ExecuteAsync(AssetId.New(), definitionId, 1);

        Assert.True(result.IsSuccess);

        var retrieved = await executionRepository.GetAsync(result.Value!.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(result.Value.Id, retrieved!.Id);
        Assert.Equal(result.Value.AssetId, retrieved.AssetId);
    }


    [Fact]
    public async Task ExecuteAsync_MissingWorkflowDefinition_ShouldReturnFailure()
    {
        var service = CreateService();

        var result = await service.ExecuteAsync(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
    }


    [Fact]
    public async Task ExecuteAsync_MissingDefinitionVersion_ShouldReturnFailure()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var service = CreateService(definitionRepository);

        var result = await service.ExecuteAsync(AssetId.New(), definitionId, 2);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
        Assert.Equal(definitionId, error.WorkflowDefinitionId);
        Assert.Equal(2, error.Version);
    }


    [Fact]
    public async Task ExecuteAsync_PersistenceRejected_ShouldReturnFailure()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var executionRepository = new RejectingWorkflowExecutionRepository();
        var service = CreateService(definitionRepository, executionRepository);

        var result = await service.ExecuteAsync(AssetId.New(), definitionId, 1);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.IsType<WorkflowExecutionPersistenceFailed>(result.Error);
    }


    [Fact]
    public async Task ExecuteAsync_EmptyAssetId_ShouldThrowDomainException()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var service = CreateService(definitionRepository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(
                new AssetId(Guid.Empty),
                definitionId,
                1));
    }


    [Fact]
    public async Task ExecuteAsync_EmptyWorkflowDefinitionId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(
                AssetId.New(),
                new WorkflowDefinitionId(Guid.Empty),
                1));
    }


    [Fact]
    public async Task ExecuteAsync_ZeroDefinitionVersion_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(
                AssetId.New(),
                WorkflowDefinitionId.New(),
                0));
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task ExecuteAsync_NegativeDefinitionVersion_ShouldThrow(
        int version)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(
                AssetId.New(),
                WorkflowDefinitionId.New(),
                version));
    }


    [Fact]
    public void Constructor_NullDefinitionRepository_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowService(
                null!,
                new InMemoryWorkflowExecutionRepository()));
    }


    [Fact]
    public void Constructor_NullExecutionRepository_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowService(
                new InMemoryWorkflowDefinitionRepository(),
                null!));
    }


    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_ShouldCancelWithoutCallingTryAdd()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var executionRepository = new TrackingWorkflowExecutionRepository();

        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var service = CreateService(definitionRepository, executionRepository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(
                AssetId.New(),
                definitionId,
                1,
                cts.Token));

        Assert.False(
            executionRepository.TryAddAsyncWasCalled,
            "TryAddAsync must not be called when the token is pre-cancelled.");
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldNotDuplicateDomainLifecycleLogic()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();

        var definitionId = WorkflowDefinitionId.New();
        await definitionRepository.TryAddAsync(CreateDefinition(definitionId, 1));

        var service = CreateService(definitionRepository, executionRepository);

        var result = await service.ExecuteAsync(AssetId.New(), definitionId, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(WorkflowExecutionStatus.Created, result.Value!.Status);
        Assert.Null(result.Value.StartedAt);
        Assert.Null(result.Value.CompletedAt);
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
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WorkflowExecution?>(null);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WorkflowExecution?>(null);
        }
    }


    private sealed class TrackingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        public bool TryAddAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            TryAddAsyncWasCalled = true;

            return Task.FromResult(true);
        }

        public Task<WorkflowExecution?> GetAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WorkflowExecution?>(null);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<WorkflowExecution?>(null);
        }
    }
}
