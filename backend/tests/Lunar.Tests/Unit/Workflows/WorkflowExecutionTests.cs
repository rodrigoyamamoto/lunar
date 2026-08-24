using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowExecutionTests
{
    [Fact]
    public void Create_ShouldCreateExecutionWithCreatedStatus()
    {
        var execution = WorkflowExecution.Create();

        Assert.Equal(
            WorkflowExecutionStatus.Created,
            execution.Status);
    }


    [Fact]
    public void Start_ShouldMoveExecutionToRunning()
    {
        var execution = WorkflowExecution.Create();

        execution.Start();

        Assert.Equal(
            WorkflowExecutionStatus.Running,
            execution.Status);

        Assert.NotNull(execution.StartedAt);
    }


    [Fact]
    public void Complete_ShouldFinishRunningExecution()
    {
        var execution = WorkflowExecution.Create();

        execution.Start();
        execution.Complete();

        Assert.Equal(
            WorkflowExecutionStatus.Completed,
            execution.Status);

        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Fail_ShouldFinishRunningExecution()
    {
        var execution = WorkflowExecution.Create();
        execution.Start();

        execution.Fail();

        Assert.Equal(
            WorkflowExecutionStatus.Failed,
            execution.Status);

        Assert.NotNull(execution.CompletedAt);
    }


    [Fact]
    public void Cancel_ShouldFinishRunningExecution()
    {
        var execution = WorkflowExecution.Create();
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
        var execution = WorkflowExecution.Create();

        execution.Complete();

        Assert.Equal(
            WorkflowExecutionStatus.Created,
            execution.Status);

        Assert.Null(execution.CompletedAt);
    }


    [Fact]
    public void Start_ShouldNotRestartCompletedExecution()
    {
        var execution = WorkflowExecution.Create();
        execution.Start();
        execution.Complete();

        execution.Start();

        Assert.Equal(
            WorkflowExecutionStatus.Completed,
            execution.Status);
    }
}
