using Lunar.Core.Artifacts;

namespace Lunar.Tests.Unit.Artifacts;

public class ArtifactIdTests
{
    [Fact]
    public void New_ShouldCreateNonEmptyIdentifier()
    {
        var artifactId = ArtifactId.New();

        Assert.NotEqual(
            Guid.Empty,
            artifactId.Value);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = ArtifactId.New();
        var second = ArtifactId.New();

        Assert.NotEqual(
            first,
            second);
    }
}