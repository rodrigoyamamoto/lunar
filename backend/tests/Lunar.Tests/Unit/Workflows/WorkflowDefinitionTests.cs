using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowDefinitionTests
{
    [Fact]
    public void ValidConstruction_ShouldPreserveIdentityNameAndOrderedSteps()
    {
        var id = WorkflowDefinitionId.New();
        var capability1 = CapabilityId.New();
        var capability2 = CapabilityId.New();

        var steps = new List<WorkflowStep>
        {
            new(1, capability1),
            new(2, capability2)
        };

        var definition = new WorkflowDefinition(
            id,
            "Character Generation",
            steps);

        Assert.Equal(id, definition.Id);
        Assert.Equal("Character Generation", definition.Name);
        Assert.Equal(2, definition.Steps.Count);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(capability1, definition.Steps[0].CapabilityId);
        Assert.Equal(2, definition.Steps[1].Position);
        Assert.Equal(capability2, definition.Steps[1].CapabilityId);
    }

    [Fact]
    public void EmptyIdentifier_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                new WorkflowDefinitionId(Guid.Empty),
                "Character Generation",
                new[] { new WorkflowStep(1, CapabilityId.New()) }));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankName_ShouldBeRejected(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                name!,
                new[] { new WorkflowStep(1, CapabilityId.New()) }));
    }

    [Fact]
    public void ZeroSteps_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                "Character Generation",
                Array.Empty<WorkflowStep>()));
    }

    [Fact]
    public void DuplicatePositions_ShouldBeRejected()
    {
        var capability = CapabilityId.New();

        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                "Character Generation",
                new[]
                {
                    new WorkflowStep(1, capability),
                    new WorkflowStep(1, CapabilityId.New())
                }));
    }

    [Fact]
    public void NonContiguousOrder_ShouldBeRejected()
    {
        var capability = CapabilityId.New();

        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                "Character Generation",
                new[]
                {
                    new WorkflowStep(1, capability),
                    new WorkflowStep(3, CapabilityId.New())
                }));
    }

    [Fact]
    public void PhysicallyOutOfOrderPositions_ShouldBeRejected()
    {
        var capability = CapabilityId.New();

        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                "Character Generation",
                new[]
                {
                    new WorkflowStep(2, capability),
                    new WorkflowStep(1, CapabilityId.New())
                }));
    }

    [Fact]
    public void ReturnedStepsCollection_CannotMutateInternalState()
    {
        var definition = new WorkflowDefinition(
            WorkflowDefinitionId.New(),
            "Character Generation",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        var steps = definition.Steps;

        Assert.IsNotType<List<WorkflowStep>>(steps);

        var act = () => ((ICollection<WorkflowStep>)steps).Add(
            new WorkflowStep(2, CapabilityId.New()));

        Assert.Throws<NotSupportedException>(act);
    }
}
