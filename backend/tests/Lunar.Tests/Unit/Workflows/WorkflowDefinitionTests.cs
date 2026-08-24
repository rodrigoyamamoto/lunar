using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowDefinitionTests
{
    private const int DefaultVersion = 1;


    [Fact]
    public void ValidConstruction_ShouldPreserveIdentityVersionNameAndOrderedSteps()
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
            DefaultVersion,
            "Character Generation",
            steps);

        Assert.Equal(id, definition.Id);
        Assert.Equal(DefaultVersion, definition.Version);
        Assert.Equal("Character Generation", definition.Name);
        Assert.Equal(2, definition.Steps.Count);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(capability1, definition.Steps[0].CapabilityId);
        Assert.Equal(2, definition.Steps[1].Position);
        Assert.Equal(capability2, definition.Steps[1].CapabilityId);
    }


    [Fact]
    public void HigherVersion_ShouldBePreserved()
    {
        var definition = new WorkflowDefinition(
            WorkflowDefinitionId.New(),
            7,
            "Character Generation",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        Assert.Equal(7, definition.Version);
    }


    [Fact]
    public void VersionZero_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                0,
                "Character Generation",
                new[] { new WorkflowStep(1, CapabilityId.New()) }));
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void NegativeVersion_ShouldBeRejected(int version)
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                version,
                "Character Generation",
                new[] { new WorkflowStep(1, CapabilityId.New()) }));
    }


    [Fact]
    public void SameDefinitionId_CanRepresentDifferentImmutableVersions()
    {
        var id = WorkflowDefinitionId.New();
        var capabilityA = CapabilityId.New();
        var capabilityB = CapabilityId.New();

        var version1 = new WorkflowDefinition(
            id,
            1,
            "Generate Character",
            new[] { new WorkflowStep(1, capabilityA) });

        var version2 = new WorkflowDefinition(
            id,
            2,
            "Generate Character Enhanced",
            new[]
            {
                new WorkflowStep(1, capabilityA),
                new WorkflowStep(2, capabilityB)
            });

        Assert.Equal(id, version1.Id);
        Assert.Equal(id, version2.Id);
        Assert.Equal(1, version1.Version);
        Assert.Equal(2, version2.Version);
    }


    [Fact]
    public void DifferentVersions_MayPreserveDifferentContents()
    {
        var id = WorkflowDefinitionId.New();
        var capabilityA = CapabilityId.New();
        var capabilityB = CapabilityId.New();

        var version1 = new WorkflowDefinition(
            id,
            1,
            "Generate Character",
            new[] { new WorkflowStep(1, capabilityA) });

        var version2 = new WorkflowDefinition(
            id,
            2,
            "Generate Character Enhanced",
            new[]
            {
                new WorkflowStep(1, capabilityA),
                new WorkflowStep(2, capabilityB)
            });

        Assert.Equal("Generate Character", version1.Name);
        Assert.Single(version1.Steps);

        Assert.Equal("Generate Character Enhanced", version2.Name);
        Assert.Equal(2, version2.Steps.Count);
    }


    [Fact]
    public void EmptyIdentifier_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                new WorkflowDefinitionId(Guid.Empty),
                DefaultVersion,
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
                DefaultVersion,
                name!,
                new[] { new WorkflowStep(1, CapabilityId.New()) }));
    }


    [Fact]
    public void ZeroSteps_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowDefinition(
                WorkflowDefinitionId.New(),
                DefaultVersion,
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
                DefaultVersion,
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
                DefaultVersion,
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
                DefaultVersion,
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
            DefaultVersion,
            "Character Generation",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        var steps = definition.Steps;

        Assert.IsNotType<List<WorkflowStep>>(steps);

        var act = () => ((ICollection<WorkflowStep>)steps).Add(
            new WorkflowStep(2, CapabilityId.New()));

        Assert.Throws<NotSupportedException>(act);
    }


    [Fact]
    public void CallerOwnedStepsCollectionMutation_ShouldNotAffectDefinition()
    {
        var capability1 = CapabilityId.New();
        var capability2 = CapabilityId.New();
        var steps = new List<WorkflowStep>
        {
            new(1, capability1),
            new(2, capability2)
        };

        var definition = new WorkflowDefinition(
            WorkflowDefinitionId.New(),
            DefaultVersion,
            "Character Generation",
            steps);

        steps.Add(new WorkflowStep(3, CapabilityId.New()));

        Assert.Equal(2, definition.Steps.Count);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(capability1, definition.Steps[0].CapabilityId);
        Assert.Equal(2, definition.Steps[1].Position);
        Assert.Equal(capability2, definition.Steps[1].CapabilityId);
    }
}
