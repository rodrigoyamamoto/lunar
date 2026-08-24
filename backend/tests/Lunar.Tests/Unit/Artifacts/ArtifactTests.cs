using Lunar.Core.Artifacts;
using Lunar.Core.Assets;

namespace Lunar.Tests.Unit.Artifacts;

public class ArtifactTests
{
    [Fact]
    public void NewArtifact_ShouldKeepProvidedInformation()
    {
        var assetId = AssetId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "corrupted-knight-concept.png",
            ArtifactType.ConceptImage);


        Assert.Equal(
            assetId,
            artifact.AssetId);


        Assert.Equal(
            "corrupted-knight-concept.png",
            artifact.Name);


        Assert.Equal(
            ArtifactType.ConceptImage,
            artifact.Type);
    }
}