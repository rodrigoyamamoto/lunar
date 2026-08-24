using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityTests
{
    [Fact]
    public void ValidConstruction_ShouldPreserveIdentityAndName()
    {
        var id = CapabilityId.New();

        var capability = new Capability(
            id,
            "Image Generation");

        Assert.Equal(id, capability.Id);
        Assert.Equal("Image Generation", capability.Name);
    }

    [Fact]
    public void EmptyIdentifier_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Capability(
                new CapabilityId(Guid.Empty),
                "Image Generation"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyOrWhitespaceName_ShouldBeRejected(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Capability(
                CapabilityId.New(),
                name!));
    }
}
