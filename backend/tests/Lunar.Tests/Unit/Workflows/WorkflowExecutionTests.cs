using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowExecutionTests
{
    private const int DefaultDefinitionVersion = 1;


    [Fact]
    public void Create_ShouldStartWithCreatedStatus()
    {
        var execution = CreateExecution();

        Assert.Equal(
            WorkflowExecutionStatus.Created,
            execution.Status);
    }

    [Fact]
    public void Create_ShouldPreserveSuppliedAssetId()
    {
        var assetId = AssetId.New();

        var execution = WorkflowExecution.Create(
            assetId,
            WorkflowDefinitionId.New(),
            DefaultDefinitionVersion);

        Assert.Equal(assetId, execution.AssetId);
    }

    [Fact]
    public void Create_ShouldPreserveSuppliedWorkflowDefinitionId()
    {
        var definitionId = WorkflowDefinitionId.New();

        var execution = WorkflowExecution.Create(
            AssetId.New(),
            definitionId,
            DefaultDefinitionVersion);

        Assert.Equal(definitionId, execution.WorkflowDefinitionId);
    }

    [Fact]
    public void Create_ShouldPreserveSuppliedWorkflowDefinitionVersion()
    {
        var execution = WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            2);

        Assert.Equal(2, execution.WorkflowDefinitionVersion);
    }

    [Fact]
    public void Create_ShouldAcceptVersionOne()
    {
        var execution = WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1);

        Assert.Equal(1, execution.WorkflowDefinitionVersion);
    }

    [Fact]
    public void Create_ShouldAcceptHigherVersion()
    {
        var execution = WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            5);

        Assert.Equal(5, execution.WorkflowDefinitionVersion);
    }

    [Fact]
    public void Create_ShouldRejectVersionZero()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Create(
                AssetId.New(),
                WorkflowDefinitionId.New(),
                0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Create_ShouldRejectNegativeVersion(int version)
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Create(
                AssetId.New(),
                WorkflowDefinitionId.New(),
                version));
    }

    [Fact]
    public void Create_ShouldRejectEmptyAssetId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Create(
                new AssetId(Guid.Empty),
                WorkflowDefinitionId.New(),
                DefaultDefinitionVersion));
    }

    [Fact]
    public void Create_ShouldRejectEmptyWorkflowDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Create(
                AssetId.New(),
                new WorkflowDefinitionId(Guid.Empty),
                DefaultDefinitionVersion));
    }


    [Fact]
    public void HistoricalReferenceStability_ExecutionRetainsExactVersion()
    {
        var definitionId = WorkflowDefinitionId.New();

        var execution = WorkflowExecution.Create(
            AssetId.New(),
            definitionId,
            1);

        // A later version of the same logical workflow is constructed.
        // The execution must still reference version 1.
        _ = new WorkflowDefinition(
            definitionId,
            2,
            "Generate Character Enhanced",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        Assert.Equal(definitionId, execution.WorkflowDefinitionId);
        Assert.Equal(1, execution.WorkflowDefinitionVersion);
    }


    [Fact]
    public void Start_ShouldMoveCreatedToRunningAndSetStartedAt()
    {
        var execution = CreateExecution();

        execution.Start();

        Assert.Equal(
            WorkflowExecutionStatus.Running,
            execution.Status);

        Assert.NotNull(execution.StartedAt);
    }


    [Fact]
    public void Complete_ShouldMoveRunningToCompletedAndSetCompletedAt()
    {
        var execution = CreateExecution();

        execution.Start();
        execution.Complete();

        Assert.Equal(
            WorkflowExecutionStatus.Completed,
            execution.Status);

        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Fail_ShouldMoveRunningToFailedAndSetCompletedAt()
    {
        var execution = CreateExecution();
        execution.Start();

        execution.Fail();

        Assert.Equal(
            WorkflowExecutionStatus.Failed,
            execution.Status);

        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Cancel_ShouldMoveRunningToCancelledAndSetCompletedAt()
    {
        var execution = CreateExecution();
        execution.Start();

        execution.Cancel();

        Assert.Equal(
            WorkflowExecutionStatus.Cancelled,
            execution.Status);

        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Complete_ShouldNotChangeCreatedExecution()
    {
        var execution = CreateExecution();

        execution.Complete();

        Assert.Equal(
            WorkflowExecutionStatus.Created,
            execution.Status);

        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Start_ShouldNotRestartCompletedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Complete();

        execution.Start();

        Assert.Equal(
            WorkflowExecutionStatus.Completed,
            execution.Status);
    }


    [Fact]
    public void Start_ShouldNotRestartFailedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Fail();

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
    }


    [Fact]
    public void Start_ShouldNotRestartCancelledExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Cancel();

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Cancelled, execution.Status);
    }


    [Fact]
    public void Start_ShouldNotChangeRunningExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        var startedAtBefore = execution.StartedAt;

        execution.Start();

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        Assert.Equal(startedAtBefore, execution.StartedAt);
    }


    [Fact]
    public void Fail_ShouldNotChangeCreatedExecution()
    {
        var execution = CreateExecution();

        execution.Fail();

        Assert.Equal(WorkflowExecutionStatus.Created, execution.Status);
        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Cancel_ShouldNotChangeCreatedExecution()
    {
        var execution = CreateExecution();

        execution.Cancel();

        Assert.Equal(WorkflowExecutionStatus.Created, execution.Status);
        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Complete_ShouldNotChangeFailedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Fail();
        var completedAtBefore = execution.CompletedAt;

        execution.Complete();

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Complete_ShouldNotChangeCancelledExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Cancel();
        var completedAtBefore = execution.CompletedAt;

        execution.Complete();

        Assert.Equal(WorkflowExecutionStatus.Cancelled, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Fail_ShouldNotChangeCompletedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Complete();
        var completedAtBefore = execution.CompletedAt;

        execution.Fail();

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Fail_ShouldNotChangeCancelledExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Cancel();
        var completedAtBefore = execution.CompletedAt;

        execution.Fail();

        Assert.Equal(WorkflowExecutionStatus.Cancelled, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Cancel_ShouldNotChangeCompletedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Complete();
        var completedAtBefore = execution.CompletedAt;

        execution.Cancel();

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Cancel_ShouldNotChangeFailedExecution()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Fail();
        var completedAtBefore = execution.CompletedAt;

        execution.Cancel();

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(completedAtBefore, execution.CompletedAt);
    }


    [Fact]
    public void Start_ShouldNotOverwriteStartedAtOnRepeatedCall()
    {
        var execution = CreateExecution();
        execution.Start();
        var startedAtOriginal = execution.StartedAt;

        execution.Start();

        Assert.Equal(startedAtOriginal, execution.StartedAt);
    }


    [Fact]
    public void Complete_ShouldNotOverwriteCompletedAtOnRepeatedCall()
    {
        var execution = CreateExecution();
        execution.Start();
        execution.Complete();
        var completedAtOriginal = execution.CompletedAt;

        execution.Complete();

        Assert.Equal(completedAtOriginal, execution.CompletedAt);
    }


    [Fact]
    public void Start_ThenComplete_ShouldPreserveStartedAt()
    {
        var execution = CreateExecution();
        execution.Start();
        var startedAtOriginal = execution.StartedAt;

        execution.Complete();

        Assert.Equal(startedAtOriginal, execution.StartedAt);
        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Start_ThenFail_ShouldPreserveStartedAt()
    {
        var execution = CreateExecution();
        execution.Start();
        var startedAtOriginal = execution.StartedAt;

        execution.Fail();

        Assert.Equal(startedAtOriginal, execution.StartedAt);
        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Start_ThenCancel_ShouldPreserveStartedAt()
    {
        var execution = CreateExecution();
        execution.Start();
        var startedAtOriginal = execution.StartedAt;

        execution.Cancel();

        Assert.Equal(startedAtOriginal, execution.StartedAt);
        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Complete_InvalidTransition_ShouldNotSetCompletedAt()
    {
        var execution = CreateExecution();

        execution.Complete();

        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Fail_InvalidTransition_ShouldNotSetCompletedAt()
    {
        var execution = CreateExecution();

        execution.Fail();

        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Cancel_InvalidTransition_ShouldNotSetCompletedAt()
    {
        var execution = CreateExecution();

        execution.Cancel();

        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void TerminalState_ShouldRejectAllTransitions()
    {
        var terminalStates = new[]
        {
            (WorkflowExecutionStatus.Completed, "Completed"),
            (WorkflowExecutionStatus.Failed, "Failed"),
            (WorkflowExecutionStatus.Cancelled, "Cancelled")
        };

        foreach (var (status, name) in terminalStates)
        {
            var execution = CreateExecution();
            execution.Start();

            if (status == WorkflowExecutionStatus.Completed)
                execution.Complete();
            else if (status == WorkflowExecutionStatus.Failed)
                execution.Fail();
            else
                execution.Cancel();

            var statusBefore = execution.Status;
            var startedAtBefore = execution.StartedAt;
            var completedAtBefore = execution.CompletedAt;

            execution.Start();
            Assert.Equal(statusBefore, execution.Status);

            execution.Complete();
            Assert.Equal(statusBefore, execution.Status);

            execution.Fail();
            Assert.Equal(statusBefore, execution.Status);

            execution.Cancel();
            Assert.Equal(statusBefore, execution.Status);

            Assert.Equal(startedAtBefore, execution.StartedAt);
            Assert.Equal(completedAtBefore, execution.CompletedAt);
        }
    }


    [Fact]
    public void LifecycleTransitions_ShouldNotChangeRevision()
    {
        var execution = CreateExecution();
        Assert.Equal(0, execution.Revision);

        execution.Start();
        Assert.Equal(0, execution.Revision);

        execution.Complete();
        Assert.Equal(0, execution.Revision);
    }


    [Fact]
    public void Create_ShouldInitializeRevisionToZero()
    {
        var execution = CreateExecution();

        Assert.Equal(0, execution.Revision);
    }


    [Fact]
    public void Start_ShouldNotIncrementRevision()
    {
        var execution = CreateExecution();

        execution.Start();

        Assert.Equal(0, execution.Revision);
    }


    [Fact]
    public void Complete_ShouldNotIncrementRevision()
    {
        var execution = CreateExecution();
        execution.Start();

        execution.Complete();

        Assert.Equal(0, execution.Revision);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructValidCreatedState()
    {
        var id = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var definitionId = WorkflowDefinitionId.New();
        var createdAt = DateTimeOffset.UtcNow;

        var execution = WorkflowExecution.Rehydrate(
            id,
            assetId,
            definitionId,
            1,
            WorkflowExecutionStatus.Created,
            0,
            createdAt,
            null,
            null);

        Assert.Equal(id, execution.Id);
        Assert.Equal(assetId, execution.AssetId);
        Assert.Equal(definitionId, execution.WorkflowDefinitionId);
        Assert.Equal(1, execution.WorkflowDefinitionVersion);
        Assert.Equal(WorkflowExecutionStatus.Created, execution.Status);
        Assert.Equal(0, execution.Revision);
        Assert.Equal(createdAt, execution.CreatedAt);
        Assert.Null(execution.StartedAt);
        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructValidRunningState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(1);

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Running,
            3,
            createdAt,
            startedAt,
            null);

        Assert.Equal(WorkflowExecutionStatus.Running, execution.Status);
        Assert.Equal(3, execution.Revision);
        Assert.Equal(startedAt, execution.StartedAt);
        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructValidCompletedState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(1);
        var completedAt = startedAt.AddMinutes(5);

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Completed,
            7,
            createdAt,
            startedAt,
            completedAt);

        Assert.Equal(WorkflowExecutionStatus.Completed, execution.Status);
        Assert.Equal(7, execution.Revision);
        Assert.Equal(startedAt, execution.StartedAt);
        Assert.Equal(completedAt, execution.CompletedAt);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructValidFailedState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(1);
        var completedAt = startedAt.AddMinutes(5);

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Failed,
            2,
            createdAt,
            startedAt,
            completedAt);

        Assert.Equal(WorkflowExecutionStatus.Failed, execution.Status);
        Assert.Equal(2, execution.Revision);
    }


    [Fact]
    public void Rehydrate_ShouldReconstructValidCancelledState()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(1);
        var completedAt = startedAt.AddMinutes(5);

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Cancelled,
            2,
            createdAt,
            startedAt,
            completedAt);

        Assert.Equal(WorkflowExecutionStatus.Cancelled, execution.Status);
        Assert.Equal(2, execution.Revision);
    }


    [Fact]
    public void Rehydrate_ShouldRejectEmptyExecutionId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                new WorkflowExecutionId(Guid.Empty),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectEmptyAssetId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                new AssetId(Guid.Empty),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectEmptyWorkflowDefinitionId()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                new WorkflowDefinitionId(Guid.Empty),
                1,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectInvalidDefinitionVersion()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                0,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void Rehydrate_ShouldRejectNegativeRevision(long revision)
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Created,
                revision,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectCreatedWithStartedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectCreatedWithCompletedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Created,
                0,
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow));
    }


    [Fact]
    public void Rehydrate_ShouldRejectRunningWithoutStartedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Running,
                1,
                DateTimeOffset.UtcNow,
                null,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectRunningWithCompletedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Running,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));
    }


    [Fact]
    public void Rehydrate_ShouldRejectTerminalWithoutStartedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Completed,
                1,
                DateTimeOffset.UtcNow,
                null,
                DateTimeOffset.UtcNow));
    }


    [Fact]
    public void Rehydrate_ShouldRejectTerminalWithoutCompletedAt()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Completed,
                1,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                null));
    }


    [Fact]
    public void Rehydrate_ShouldRejectRunningStartedBeforeCreated()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(-1);

        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                WorkflowExecutionStatus.Running,
                1,
                createdAt,
                startedAt,
                null));
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public void Rehydrate_ShouldRejectTerminalStartedBeforeCreated(
        WorkflowExecutionStatus terminalStatus)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(-1);
        var completedAt = createdAt.AddMinutes(5);

        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                terminalStatus,
                1,
                createdAt,
                startedAt,
                completedAt));
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public void Rehydrate_ShouldRejectTerminalCompletedBeforeStarted(
        WorkflowExecutionStatus terminalStatus)
    {
        var createdAt = DateTimeOffset.UtcNow;
        var startedAt = createdAt.AddMinutes(5);
        var completedAt = startedAt.AddMinutes(-1);

        Assert.Throws<ArgumentException>(() =>
            WorkflowExecution.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                WorkflowDefinitionId.New(),
                1,
                terminalStatus,
                1,
                createdAt,
                startedAt,
                completedAt));
    }


    [Fact]
    public void Rehydrate_ShouldAcceptEqualTimestampsForTerminalState()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Completed,
            1,
            timestamp,
            timestamp,
            timestamp);

        Assert.Equal(timestamp, execution.CreatedAt);
        Assert.Equal(timestamp, execution.StartedAt);
        Assert.Equal(timestamp, execution.CompletedAt);
    }


    [Fact]
    public void Rehydrate_ShouldAcceptEqualTimestampsForRunningState()
    {
        var timestamp = DateTimeOffset.UtcNow;

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Running,
            1,
            timestamp,
            timestamp,
            null);

        Assert.Equal(timestamp, execution.CreatedAt);
        Assert.Equal(timestamp, execution.StartedAt);
        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Rehydrate_ShouldPreserveExactSuppliedTimestamps()
    {
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var startedAt = new DateTimeOffset(2026, 1, 15, 10, 5, 0, TimeSpan.Zero);
        var completedAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var execution = WorkflowExecution.Rehydrate(
            WorkflowExecutionId.New(),
            AssetId.New(),
            WorkflowDefinitionId.New(),
            1,
            WorkflowExecutionStatus.Completed,
            4,
            createdAt,
            startedAt,
            completedAt);

        Assert.Equal(createdAt, execution.CreatedAt);
        Assert.Equal(startedAt, execution.StartedAt);
        Assert.Equal(completedAt, execution.CompletedAt);
    }


    private static WorkflowExecution CreateExecution()
    {
        return WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            DefaultDefinitionVersion);
    }
}
