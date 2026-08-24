using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var capabilityId = CapabilityId.New();

        Assert.NotEqual(
            Guid.Empty,
            capabilityId.Value);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = CapabilityId.New();
        var second = CapabilityId.New();

        Assert.NotEqual(
            first,
            second);
    }
}
