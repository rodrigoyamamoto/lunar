using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionSucceededTests
{
    private static CapabilityExecutionOutput CreateOutput()
    {
        return new CapabilityExecutionOutput(
            new BinaryArtifactContent(new byte[] { 0xFF, 0xD8, 0xFF }, "image/jpeg"));
    }


    [Fact]
    public void Constructor_ValidOutput_ShouldPreserveExactInstance()
    {
        var output = CreateOutput();

        var succeeded = new CapabilityExecutionSucceeded(output);

        Assert.Same(output, succeeded.Output);
    }


    [Fact]
    public void Constructor_NullOutput_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionSucceeded(null!));
    }


    [Fact]
    public void Succeeded_ShouldBeAssignableToOutcome()
    {
        var succeeded = new CapabilityExecutionSucceeded(CreateOutput());

        Assert.IsAssignableFrom<CapabilityExecutionOutcome>(succeeded);
    }
}
