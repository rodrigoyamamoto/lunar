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
}