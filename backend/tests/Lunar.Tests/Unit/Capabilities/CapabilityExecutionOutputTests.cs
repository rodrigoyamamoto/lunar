using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionOutputTests
{
    [Fact]
    public void Constructor_ValidValues_ShouldPreserveNameAndType()
    {
        var output = new CapabilityExecutionOutput(
            "knight-concept.png",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());

        Assert.Equal("knight-concept.png", output.ArtifactName);
        Assert.Equal(ArtifactType.ConceptImage, output.ArtifactType);
    }


    [Fact]
    public void Constructor_NullArtifactName_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionOutput(
                null!,
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }


    [Fact]
    public void Constructor_NullSourceArtifactIds_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionOutput(
                "name.png",
                ArtifactType.ConceptImage,
                null!));
    }


    [Fact]
    public void Constructor_EmptyName_ShouldBeAllowedByOutputContract()
    {
        var output = new CapabilityExecutionOutput(
            "",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());

        Assert.Equal("", output.ArtifactName);
    }


    [Fact]
    public void Constructor_WhitespaceName_ShouldBeAllowedByOutputContract()
    {
        var output = new CapabilityExecutionOutput(
            "   ",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());

        Assert.Equal("   ", output.ArtifactName);
    }


    [Fact]
    public void Constructor_SourceArtifactIds_ShouldPreserveOrder()
    {
        var a = ArtifactId.New();
        var b = ArtifactId.New();
        var c = ArtifactId.New();

        var output = new CapabilityExecutionOutput(
            "name.png",
            ArtifactType.ConceptImage,
            new[] { a, b, c });

        Assert.Equal(3, output.SourceArtifactIds.Count);
        Assert.Equal(a, output.SourceArtifactIds[0]);
        Assert.Equal(b, output.SourceArtifactIds[1]);
        Assert.Equal(c, output.SourceArtifactIds[2]);
    }


    [Fact]
    public void Constructor_CallerCollectionMutation_ShouldNotAlterOutput()
    {
        var sourceIds = new List<ArtifactId> { ArtifactId.New(), ArtifactId.New() };
        var output = new CapabilityExecutionOutput(
            "name.png",
            ArtifactType.ConceptImage,
            sourceIds);

        sourceIds.Add(ArtifactId.New());
        sourceIds.RemoveAt(0);

        Assert.Equal(2, output.SourceArtifactIds.Count);
    }


    [Fact]
    public void Constructor_ExposedCollection_ShouldBeReadOnly()
    {
        var output = new CapabilityExecutionOutput(
            "name.png",
            ArtifactType.ConceptImage,
            new[] { ArtifactId.New() });

        Assert.IsAssignableFrom<IReadOnlyList<ArtifactId>>(output.SourceArtifactIds);
        Assert.Throws<NotSupportedException>(() =>
            ((ICollection<ArtifactId>)output.SourceArtifactIds).Add(ArtifactId.New()));
    }
}
