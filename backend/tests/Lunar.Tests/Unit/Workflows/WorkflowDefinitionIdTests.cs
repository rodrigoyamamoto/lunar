using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowDefinitionIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var definitionId = WorkflowDefinitionId.New();

        Assert.NotEqual(
            Guid.Empty,
            definitionId.Value);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = WorkflowDefinitionId.New();
        var second = WorkflowDefinitionId.New();

        Assert.NotEqual(
            first,
            second);
    }
}
