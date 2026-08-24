using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

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


        Assert.Null(artifact.SourceExecutionId);
    }


    [Fact]
    public void NewArtifact_ShouldKeepSourceExecutionWhenProvided()
    {
        var executionId = WorkflowExecutionId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "corrupted-knight-concept.png",
            ArtifactType.ConceptImage,
            executionId);


        Assert.Equal(
            executionId,
            artifact.SourceExecutionId);
    }
}
