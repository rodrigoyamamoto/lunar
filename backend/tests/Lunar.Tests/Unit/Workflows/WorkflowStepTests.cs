using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class WorkflowStepTests
{
    [Fact]
    public void ValidConstruction_ShouldPreservePosition()
    {
        var capabilityId = CapabilityId.New();

        var step = new WorkflowStep(1, capabilityId);

        Assert.Equal(1, step.Position);
    }

    [Fact]
    public void ValidConstruction_ShouldPreserveCapabilityId()
    {
        var capabilityId = CapabilityId.New();

        var step = new WorkflowStep(1, capabilityId);

        Assert.Equal(capabilityId, step.CapabilityId);
    }

    [Fact]
    public void PositionZero_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStep(0, CapabilityId.New()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public void NegativePosition_ShouldBeRejected(int position)
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStep(position, CapabilityId.New()));
    }

    [Fact]
    public void EmptyCapabilityId_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStep(1, new CapabilityId(Guid.Empty)));
    }
}
