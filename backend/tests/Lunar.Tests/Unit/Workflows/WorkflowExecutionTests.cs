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


    private static WorkflowExecution CreateExecution()
    {
        return WorkflowExecution.Create(
            AssetId.New(),
            WorkflowDefinitionId.New(),
            DefaultDefinitionVersion);
    }
}
