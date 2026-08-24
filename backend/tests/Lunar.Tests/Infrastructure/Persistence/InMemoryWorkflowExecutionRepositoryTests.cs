using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Infrastructure.Persistence;

public class InMemoryWorkflowExecutionRepositoryTests
{
    private static WorkflowExecution CreateExecution()
    {
        return WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1);
    }


    [Fact]
    public async Task TryAddAsync_FirstInsertion_ShouldSucceed()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        var result = await repository.TryAddAsync(execution);

        Assert.True(result);
    }


    [Fact]
    public async Task TryAddAsync_DuplicateId_ShouldReturnFalse()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var second = await repository.TryAddAsync(execution);

        Assert.False(second);
    }


    [Fact]
    public async Task TryAddAsync_Duplicate_ShouldNotOverwriteStoredExecution()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var id = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var definitionId = WorkflowDefinitionId.New();

        var original = WorkflowExecution.Create(assetId, definitionId, 1);
        var originalId = original.Id;

        await repository.TryAddAsync(original);

        var retrieved = await repository.GetAsync(originalId);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Created, retrieved!.Status);
    }


    [Fact]
    public async Task GetAsync_ExistingExecution_ShouldRetrieveById()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(execution.Id, retrieved!.Id);
        Assert.Equal(execution.AssetId, retrieved.AssetId);
        Assert.Equal(execution.WorkflowDefinitionId, retrieved.WorkflowDefinitionId);
        Assert.Equal(execution.WorkflowDefinitionVersion, retrieved.WorkflowDefinitionVersion);
        Assert.Equal(WorkflowExecutionStatus.Created, retrieved.Status);
        Assert.Equal(0, retrieved.Revision);
    }


    [Fact]
    public async Task GetAsync_UnknownValidId_ShouldReturnNull()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        var result = await repository.GetAsync(WorkflowExecutionId.New());

        Assert.Null(result);
    }


    [Fact]
    public async Task GetAsync_EmptyId_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(new WorkflowExecutionId(Guid.Empty)));
    }


    [Fact]
    public async Task TryAddAsync_NullExecution_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryAddAsync(null!));
    }


    [Fact]
    public async Task TryAddAsync_NonZeroRevision_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Created,
            5,
            DateTimeOffset.UtcNow,
            null,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryAddAsync(execution));
    }


    [Fact]
    public async Task TryAddAsync_OriginalObjectMutation_ShouldNotAffectStoredState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        execution.Start();

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(WorkflowExecutionStatus.Created, retrieved!.Status);
        Assert.Equal(0, retrieved.Revision);
        Assert.Null(retrieved.StartedAt);
    }


    [Fact]
    public async Task GetAsync_ReturnedObjectMutation_ShouldNotAffectStoredState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);

        Assert.NotNull(retrieved);
        retrieved!.Start();

        var second = await repository.GetAsync(execution.Id);

        Assert.NotNull(second);
        Assert.Equal(WorkflowExecutionStatus.Created, second!.Status);
        Assert.Equal(0, second.Revision);
        Assert.Null(second.StartedAt);
    }


    [Fact]
    public async Task TryUpdateAsync_CreatedToRunning_ShouldSucceedAndIncrementRevision()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();

        var updated = await repository.TryUpdateAsync(retrieved, 0);

        Assert.NotNull(updated);
        Assert.Equal(WorkflowExecutionStatus.Running, updated!.Status);
        Assert.Equal(1, updated.Revision);
        Assert.NotNull(updated.StartedAt);
        Assert.Null(updated.CompletedAt);
    }


    [Fact]
    public async Task TryUpdateAsync_Success_ShouldPersistUpdatedState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();

        await repository.TryUpdateAsync(retrieved, 0);

        var after = await repository.GetAsync(execution.Id);

        Assert.NotNull(after);
        Assert.Equal(WorkflowExecutionStatus.Running, after!.Status);
        Assert.Equal(1, after.Revision);
    }


    [Fact]
    public async Task TryUpdateAsync_RunningToCompleted_ShouldSucceed()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();
        await repository.TryUpdateAsync(retrieved, 0);

        var running = await repository.GetAsync(execution.Id);
        running!.Complete();

        var updated = await repository.TryUpdateAsync(running, 1);

        Assert.NotNull(updated);
        Assert.Equal(WorkflowExecutionStatus.Completed, updated!.Status);
        Assert.Equal(2, updated.Revision);
        Assert.NotNull(updated.CompletedAt);
    }


    [Fact]
    public async Task TryUpdateAsync_RunningToFailed_ShouldSucceed()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();
        await repository.TryUpdateAsync(retrieved, 0);

        var running = await repository.GetAsync(execution.Id);
        running!.Fail();

        var updated = await repository.TryUpdateAsync(running, 1);

        Assert.NotNull(updated);
        Assert.Equal(WorkflowExecutionStatus.Failed, updated!.Status);
        Assert.Equal(2, updated.Revision);
    }


    [Fact]
    public async Task TryUpdateAsync_RunningToCancelled_ShouldSucceed()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();
        await repository.TryUpdateAsync(retrieved, 0);

        var running = await repository.GetAsync(execution.Id);
        running!.Cancel();

        var updated = await repository.TryUpdateAsync(running, 1);

        Assert.NotNull(updated);
        Assert.Equal(WorkflowExecutionStatus.Cancelled, updated!.Status);
        Assert.Equal(2, updated.Revision);
    }


    [Fact]
    public async Task TryUpdateAsync_StaleRevision_ShouldFail()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var copyA = await repository.GetAsync(execution.Id);
        var copyB = await repository.GetAsync(execution.Id);

        copyA!.Start();
        copyB!.Start();

        var first = await repository.TryUpdateAsync(copyA, 0);
        var second = await repository.TryUpdateAsync(copyB, 0);

        Assert.NotNull(first);
        Assert.Null(second);

        var stored = await repository.GetAsync(execution.Id);
        Assert.Equal(1, stored!.Revision);
        Assert.Equal(WorkflowExecutionStatus.Running, stored.Status);
    }


    [Fact]
    public async Task TryUpdateAsync_MissingExecution_ShouldReturnNull()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        var result = await repository.TryUpdateAsync(execution, 0);

        Assert.Null(result);
    }


    [Fact]
    public async Task TryUpdateAsync_NullExecution_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryUpdateAsync(null!, 0));
    }


    [Fact]
    public async Task TryUpdateAsync_AlteredAssetId_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var altered = WorkflowExecution.Rehydrate(
            execution.Id,
            AssetId.New(),
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            WorkflowExecutionStatus.Running,
            0,
            execution.CreatedAt,
            DateTimeOffset.UtcNow,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(altered, 0));
    }


    [Fact]
    public async Task TryUpdateAsync_AlteredWorkflowDefinitionId_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var altered = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            WorkflowDefinitionId.New(),
            execution.WorkflowDefinitionVersion,
            WorkflowExecutionStatus.Running,
            0,
            execution.CreatedAt,
            DateTimeOffset.UtcNow,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(altered, 0));
    }


    [Fact]
    public async Task TryUpdateAsync_AlteredWorkflowDefinitionVersion_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var altered = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            99,
            WorkflowExecutionStatus.Running,
            0,
            execution.CreatedAt,
            DateTimeOffset.UtcNow,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(altered, 0));
    }


    [Fact]
    public async Task TryUpdateAsync_AlteredCreatedAt_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var altered = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            WorkflowExecutionStatus.Running,
            0,
            execution.CreatedAt.AddYears(-1),
            DateTimeOffset.UtcNow,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(altered, 0));
    }


    [Fact]
    public async Task TryUpdateAsync_RunningToCreated_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();
        await repository.TryUpdateAsync(retrieved, 0);

        var running = await repository.GetAsync(execution.Id);

        var rolledBack = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            WorkflowExecutionStatus.Created,
            1,
            execution.CreatedAt,
            null,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(rolledBack, 1));
    }


    [Fact]
    public async Task TryUpdateAsync_CompletedToRunning_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();
        await repository.TryUpdateAsync(retrieved, 0);

        var running = await repository.GetAsync(execution.Id);
        running!.Complete();
        await repository.TryUpdateAsync(running, 1);

        var rolledBack = WorkflowExecution.Rehydrate(
            execution.Id,
            execution.AssetId,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            WorkflowExecutionStatus.Running,
            2,
            execution.CreatedAt,
            DateTimeOffset.UtcNow,
            null);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.TryUpdateAsync(rolledBack, 2));
    }


    [Fact]
    public async Task TryUpdateAsync_NoOp_ShouldReturnCurrentStateWithoutIncrementing()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);

        var result = await repository.TryUpdateAsync(retrieved!, 0);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Revision);
        Assert.Equal(WorkflowExecutionStatus.Created, result.Status);
    }


    [Fact]
    public async Task TryUpdateAsync_UpdatedObjectMutation_ShouldNotAffectStoredState()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();

        var updated = await repository.TryUpdateAsync(retrieved, 0);

        Assert.NotNull(updated);
        updated!.Complete();

        var stored = await repository.GetAsync(execution.Id);

        Assert.NotNull(stored);
        Assert.Equal(WorkflowExecutionStatus.Running, stored!.Status);
        Assert.Equal(1, stored.Revision);
        Assert.Null(stored.CompletedAt);
    }


    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldNotAdd()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(execution, cts.Token));

        var retrieved = await repository.GetAsync(execution.Id);
        Assert.Null(retrieved);
    }


    [Fact]
    public async Task GetAsync_PreCancelledToken_ShouldCancel()
    {
        var repository = new InMemoryWorkflowExecutionRepository();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAsync(WorkflowExecutionId.New(), cts.Token));
    }


    [Fact]
    public async Task TryUpdateAsync_PreCancelledToken_ShouldNotUpdateOrIncrement()
    {
        var repository = new InMemoryWorkflowExecutionRepository();
        var execution = CreateExecution();

        await repository.TryAddAsync(execution);

        var retrieved = await repository.GetAsync(execution.Id);
        retrieved!.Start();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryUpdateAsync(retrieved, 0, cts.Token));

        var stored = await repository.GetAsync(execution.Id);
        Assert.NotNull(stored);
        Assert.Equal(0, stored!.Revision);
        Assert.Equal(WorkflowExecutionStatus.Created, stored.Status);
    }
}
