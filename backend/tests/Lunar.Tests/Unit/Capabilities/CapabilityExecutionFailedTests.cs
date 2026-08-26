using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionFailedTests
{
    [Fact]
    public void Constructor_ValidFailure_ShouldPreserveExactInstance()
    {
        var failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.QuotaExhausted);

        var failed = new CapabilityExecutionFailed(failure);

        Assert.Same(failure, failed.Failure);
    }


    [Fact]
    public void Constructor_NullFailure_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionFailed(null!));
    }


    [Fact]
    public void Failed_ShouldBeAssignableToOutcome()
    {
        var failed = new CapabilityExecutionFailed(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.Rejected));

        Assert.IsAssignableFrom<CapabilityExecutionOutcome>(failed);
    }
}
