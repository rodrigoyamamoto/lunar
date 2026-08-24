using Lunar.Core.Primitives;

namespace Lunar.Tests.Unit.Primitives;

public class IdGeneratorTests
{
    [Fact]
    public void New_ShouldCreateVersionSevenIdentifier()
    {
        var id = IdGenerator.New();

        Assert.Equal(7, id.Version);
    }


    [Fact]
    public void New_ShouldCreateDifferentIdentifiers()
    {
        var first = IdGenerator.New();
        var second = IdGenerator.New();

        Assert.NotEqual(first, second);
    }
}
