using Lunar.Application;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Workflows;

public class StartWorkflowExecutionServiceTests
{
    private static WorkflowExecution CreateCreatedExecution()
    {
        return WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1);
    }


    private static async Task<WorkflowExecution> PersistCreatedExecutionAsync(
        IWorkflowExecutionRepository repository)
    {
        var execution = CreateCreatedExecution();
        await repository.TryAddAsync(execution);
        return await repository.GetAsync(execution.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistRunningExecutionAsync(
        IWorkflowExecutionRepository repository)
    {
        var execution = await PersistCreatedExecutionAsync(repository);
        execution.Start();
        await repository.TryUpdateAsync(execution, 0);
        return await repository.GetAsync(execution.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistTerminalExecutionAsync(
        IWorkflowExecutionRepository repository,
        WorkflowExecutionStatus terminalStatus)
    {
        var running = await PersistRunningExecutionAsync(repository);
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


    private static StartWorkflowExecutionService CreateService(
        IWorkflowExecutionRepository? executionRepository = null)
    {
        return new StartWorkflowExecutionService(
            executionRepository ?? new InMemoryWorkflowExecutionRepository());
    }


    [Fact]
    public async Task StartAsync_CreatedExecution_ShouldReturnSuccessWithRunningStatus()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        var result = await service.StartAsync(execution.Id, 0);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(WorkflowExecutionStatus.Running, result.Value!.Status);
        Assert.NotNull(result.Value.StartedAt);
        Assert.Null(result.Value.CompletedAt);
        Assert.Equal(1, result.Value.Revision);
    }


    [Fact]
    public async Task StartAsync_Success_ShouldReturnPersistedRevision()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        var result = await service.StartAsync(execution.Id, 0);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Revision);
    }


    [Fact]
    public async Task StartAsync_Success_ShouldPersistRunningState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        await service.StartAsync(execution.Id, 0);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Running, retrieved!.Status);
        Assert.Equal(1, retrieved.Revision);
        Assert.NotNull(retrieved.StartedAt);
        Assert.Null(retrieved.CompletedAt);
    }


    [Fact]
    public async Task StartAsync_MissingExecution_ShouldReturnNotFoundWithExactId()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var service = CreateService(repository);

        var executionId = WorkflowExecutionId.New();

        var result = await service.StartAsync(executionId, 0);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionNotFound>(result.Error);
        Assert.Equal(executionId, error.WorkflowExecutionId);
    }


    [Fact]
    public async Task StartAsync_MissingExecution_ShouldNotCallTryUpdate()
    {
        var trackingRepository = new TrackingWorkflowExecutionRepository();
        var service = CreateService(trackingRepository);

        await service.StartAsync(WorkflowExecutionId.New(), 0);

        Assert.False(
            trackingRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must not be called when the execution is missing.");
    }


    [Fact]
    public async Task StartAsync_StaleRevisionAtRead_ShouldReturnConcurrencyConflict()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        var result = await service.StartAsync(execution.Id, 99);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionConcurrencyConflict>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(99, error.ExpectedRevision);
    }


    [Fact]
    public async Task StartAsync_StaleRevisionAtRead_ShouldNotCallTryUpdate()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);
        var trackingRepository = new TrackingWorkflowExecutionRepository(execution);
        var service = CreateService(trackingRepository);

        await service.StartAsync(execution.Id, 99);

        Assert.False(
            trackingRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must not be called when the revision is already stale.");
    }


    [Fact]
    public async Task StartAsync_StaleRevisionAtRead_ShouldNotChangePersistedState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        await service.StartAsync(execution.Id, 99);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Created, retrieved!.Status);
        Assert.Equal(0, retrieved.Revision);
    }


    [Fact]
    public async Task StartAsync_RaceConflictAtUpdate_ShouldReturnConcurrencyConflict()
    {
        var execution = CreateCreatedExecution();
        var raceRepository = new RacingWorkflowExecutionRepository(execution, 0);
        var service = CreateService(raceRepository);

        var result = await service.StartAsync(execution.Id, 0);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionConcurrencyConflict>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(0, error.ExpectedRevision);
    }


    [Fact]
    public async Task StartAsync_RunningExecution_ShouldReturnCannotStart()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistRunningExecutionAsync(repository);

        var service = CreateService(repository);

        var result = await service.StartAsync(execution.Id, execution.Revision);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionCannotStart>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Running, error.CurrentStatus);
    }


    [Fact]
    public async Task StartAsync_RunningExecution_ShouldNotCallTryUpdate()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistRunningExecutionAsync(repository);
        var trackingRepository = new TrackingWorkflowExecutionRepository(execution);
        var service = CreateService(trackingRepository);

        await service.StartAsync(execution.Id, execution.Revision);

        Assert.False(
            trackingRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must not be called when the execution cannot start.");
    }


    [Fact]
    public async Task StartAsync_RunningExecution_ShouldNotOverwriteStartedAt()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistRunningExecutionAsync(repository);
        var originalStartedAt = execution.StartedAt;

        var service = CreateService(repository);

        await service.StartAsync(execution.Id, execution.Revision);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(originalStartedAt, retrieved!.StartedAt);
        Assert.Equal(execution.Revision, retrieved.Revision);
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public async Task StartAsync_TerminalExecution_ShouldReturnCannotStart(
        WorkflowExecutionStatus terminalStatus)
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistTerminalExecutionAsync(repository, terminalStatus);

        var service = CreateService(repository);

        var result = await service.StartAsync(execution.Id, execution.Revision);

        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        var error = Assert.IsType<WorkflowExecutionCannotStart>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(terminalStatus, error.CurrentStatus);
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public async Task StartAsync_TerminalExecution_ShouldNotCallTryUpdate(
        WorkflowExecutionStatus terminalStatus)
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistTerminalExecutionAsync(repository, terminalStatus);
        var trackingRepository = new TrackingWorkflowExecutionRepository(execution);
        var service = CreateService(trackingRepository);

        await service.StartAsync(execution.Id, execution.Revision);

        Assert.False(
            trackingRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must not be called for terminal executions.");
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public async Task StartAsync_TerminalExecution_ShouldNotAdvanceRevision(
        WorkflowExecutionStatus terminalStatus)
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistTerminalExecutionAsync(repository, terminalStatus);
        var originalRevision = execution.Revision;

        var service = CreateService(repository);

        await service.StartAsync(execution.Id, execution.Revision);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(originalRevision, retrieved!.Revision);
    }


    [Fact]
    public async Task StartAsync_EmptyExecutionId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(new WorkflowExecutionId(Guid.Empty), 0));
    }


    [Fact]
    public async Task StartAsync_NegativeExpectedRevision_WithExistingExecution_ShouldThrow()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);

        var service = CreateService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(execution.Id, -1));
    }


    [Fact]
    public async Task StartAsync_NegativeExpectedRevision_WithMissingExecution_ShouldThrow()
    {
        var trackingRepository = new TrackingWorkflowExecutionRepository();
        var service = CreateService(trackingRepository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.StartAsync(WorkflowExecutionId.New(), -1));

        Assert.False(
            trackingRepository.GetAsyncWasCalled,
            "GetAsync must not be called when expectedRevision is negative.");
    }


    [Fact]
    public async Task StartAsync_PreCancelledTokenBeforeRead_ShouldThrowAndNotCallTryUpdate()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = await PersistCreatedExecutionAsync(repository);
        var trackingRepository = new TrackingWorkflowExecutionRepository(execution);
        var service = CreateService(trackingRepository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.StartAsync(execution.Id, 0, cts.Token));

        Assert.False(
            trackingRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must not be called when the token is pre-cancelled.");
    }


    [Fact]
    public async Task StartAsync_TokenCancelledAfterRead_ShouldThrowAtUpdateBoundary()
    {
        var execution = CreateCreatedExecution();
        using var cts = new CancellationTokenSource();
        var cancelBeforeUpdateRepository =
            new CancelBeforeUpdateWorkflowExecutionRepository(execution, cts);
        var service = CreateService(cancelBeforeUpdateRepository);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.StartAsync(execution.Id, 0, cts.Token));

        Assert.True(
            cancelBeforeUpdateRepository.GetAsyncWasCalled,
            "GetAsync must have completed successfully before cancellation.");
        Assert.True(
            cancelBeforeUpdateRepository.TryUpdateAsyncWasCalled,
            "TryUpdateAsync must have been reached after the read succeeded.");
    }


    [Fact]
    public void Constructor_NullRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new StartWorkflowExecutionService(null!));
    }


    private sealed class TrackingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        private readonly WorkflowExecution? _execution;

        public TrackingWorkflowExecutionRepository(WorkflowExecution? execution = null)
        {
            _execution = execution;
        }

        public bool TryUpdateAsyncWasCalled { get; private set; }

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

            return Task.FromResult<WorkflowExecution?>(
                _execution is null ? null : RehydrateSnapshot(_execution));
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

            TryUpdateAsyncWasCalled = true;

            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(execution));
        }


        private static WorkflowExecution RehydrateSnapshot(WorkflowExecution execution)
        {
            return WorkflowExecution.Rehydrate(
                execution.Id,
                execution.AssetId,
                execution.WorkflowDefinitionId,
                execution.WorkflowDefinitionVersion,
                execution.Status,
                execution.Revision,
                execution.CreatedAt,
                execution.StartedAt,
                execution.CompletedAt);
        }
    }


    private sealed class RacingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        private readonly WorkflowExecution _execution;
        private readonly long _expectedRevision;

        public RacingWorkflowExecutionRepository(
            WorkflowExecution execution,
            long expectedRevision)
        {
            _execution = execution;
            _expectedRevision = expectedRevision;
        }

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

            if (id != _execution.Id)
            {
                return Task.FromResult<WorkflowExecution?>(null);
            }

            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(_execution));
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


        private static WorkflowExecution RehydrateSnapshot(WorkflowExecution execution)
        {
            return WorkflowExecution.Rehydrate(
                execution.Id,
                execution.AssetId,
                execution.WorkflowDefinitionId,
                execution.WorkflowDefinitionVersion,
                execution.Status,
                execution.Revision,
                execution.CreatedAt,
                execution.StartedAt,
                execution.CompletedAt);
        }
    }


    private sealed class CancelBeforeUpdateWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        private readonly WorkflowExecution _execution;
        private readonly CancellationTokenSource _cts;

        public CancelBeforeUpdateWorkflowExecutionRepository(
            WorkflowExecution execution,
            CancellationTokenSource cts)
        {
            _execution = execution;
            _cts = cts;
        }

        public bool GetAsyncWasCalled { get; private set; }

        public bool TryUpdateAsyncWasCalled { get; private set; }

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

            if (id != _execution.Id)
            {
                return Task.FromResult<WorkflowExecution?>(null);
            }

            GetAsyncWasCalled = true;

            _cts.Cancel();

            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(_execution));
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);

            TryUpdateAsyncWasCalled = true;

            cancellationToken.ThrowIfCancellationRequested();

            if (expectedRevision < 0)
            {
                throw new ArgumentException(
                    "Expected revision cannot be negative.",
                    nameof(expectedRevision));
            }

            return Task.FromResult<WorkflowExecution?>(RehydrateSnapshot(execution));
        }


        private static WorkflowExecution RehydrateSnapshot(WorkflowExecution execution)
        {
            return WorkflowExecution.Rehydrate(
                execution.Id,
                execution.AssetId,
                execution.WorkflowDefinitionId,
                execution.WorkflowDefinitionVersion,
                execution.Status,
                execution.Revision,
                execution.CreatedAt,
                execution.StartedAt,
                execution.CompletedAt);
        }
    }
}
