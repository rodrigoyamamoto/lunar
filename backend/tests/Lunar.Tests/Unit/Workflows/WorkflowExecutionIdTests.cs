using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowExecutionIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var executionId = WorkflowExecutionId.New();

        Assert.NotEqual(
            Guid.Empty,
            executionId.Value);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = WorkflowExecutionId.New();
        var second = WorkflowExecutionId.New();

        Assert.NotEqual(
            first,
            second);
    }
}
