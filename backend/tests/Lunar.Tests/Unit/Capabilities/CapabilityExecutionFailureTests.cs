using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionFailureTests
{
    public static IEnumerable<object[]> AllKinds =>
        Enum.GetValues<CapabilityExecutionFailureKind>()
            .Select(k => new object[] { k });


    [Theory]
    [MemberData(nameof(AllKinds))]
    public void Constructor_EveryKindWithoutRetryAfter_ShouldPreserveKind(
        CapabilityExecutionFailureKind kind)
    {
        var failure = new CapabilityExecutionFailure(kind);

        Assert.Equal(kind, failure.Kind);
        Assert.Null(failure.RetryAfter);
    }


    [Fact]
    public void Constructor_PositiveRetryAfter_ShouldPreserveExactly()
    {
        var duration = TimeSpan.FromSeconds(42);

        var failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.RateLimited,
            duration);

        Assert.Equal(duration, failure.RetryAfter);
    }


    [Fact]
    public void Constructor_ZeroRetryAfter_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CapabilityExecutionFailure(
                CapabilityExecutionFailureKind.RateLimited,
                TimeSpan.Zero));
    }


    [Fact]
    public void Constructor_NegativeRetryAfter_ShouldThrow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CapabilityExecutionFailure(
                CapabilityExecutionFailureKind.RateLimited,
                TimeSpan.FromSeconds(-1)));
    }


    [Fact]
    public void Constructor_UndefinedEnumKind_ShouldThrow()
    {
        var undefinedKind = (CapabilityExecutionFailureKind)int.MaxValue;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new CapabilityExecutionFailure(undefinedKind));
    }
}
