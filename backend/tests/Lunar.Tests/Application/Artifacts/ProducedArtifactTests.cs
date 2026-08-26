using Lunar.Application.Artifacts;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Application.Artifacts;

public class ProducedArtifactTests
{
    private static Artifact CreateArtifact()
    {
        return new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "knight-concept.png",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            WorkflowExecutionId.New());
    }

    private static ArtifactContent CreateContent()
    {
        return new BinaryArtifactContent(
            new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF },
            "image/png");
    }


    [Fact]
    public void Constructor_ValidValues_ShouldPreserveArtifactAndContent()
    {
        var artifact = CreateArtifact();
        var content = CreateContent();

        var produced = new ProducedArtifact(artifact, content);

        Assert.Same(artifact, produced.Artifact);
        Assert.Same(content, produced.Content);
    }


    [Fact]
    public void Constructor_NullArtifact_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProducedArtifact(null!, CreateContent()));
    }


    [Fact]
    public void Constructor_NullContent_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ProducedArtifact(CreateArtifact(), null!));
    }
}
