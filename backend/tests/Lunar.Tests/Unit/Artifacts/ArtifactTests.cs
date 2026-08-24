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
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());


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
        Assert.Empty(artifact.SourceArtifactIds);
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
            Array.Empty<ArtifactId>(),
            executionId);


        Assert.Equal(
            executionId,
            artifact.SourceExecutionId);
    }


    [Fact]
    public void ValidConstruction_ShouldPreserveAllProvidedInformation()
    {
        var id = ArtifactId.New();
        var assetId = AssetId.New();
        var executionId = WorkflowExecutionId.New();
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();

        var artifact = new Artifact(
            id,
            assetId,
            "Ancient  Gate Texture",
            ArtifactType.Texture,
            new[] { sourceA, sourceB },
            executionId);


        Assert.Equal(id, artifact.Id);
        Assert.Equal(assetId, artifact.AssetId);
        Assert.Equal("Ancient  Gate Texture", artifact.Name);
        Assert.Equal(ArtifactType.Texture, artifact.Type);
        Assert.Equal(executionId, artifact.SourceExecutionId);
        Assert.Equal(2, artifact.SourceArtifactIds.Count);
        Assert.Equal(sourceA, artifact.SourceArtifactIds[0]);
        Assert.Equal(sourceB, artifact.SourceArtifactIds[1]);
    }


    [Fact]
    public void SourcelessArtifact_ShouldBeValidWithEmptyLineage()
    {
        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "imported-reference.png",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());


        Assert.Null(artifact.SourceExecutionId);
        Assert.Empty(artifact.SourceArtifactIds);
    }


    [Fact]
    public void WorkflowProducedArtifact_WithoutSources_ShouldBeValid()
    {
        var executionId = WorkflowExecutionId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "generated-concept.png",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            executionId);


        Assert.Equal(executionId, artifact.SourceExecutionId);
        Assert.Empty(artifact.SourceArtifactIds);
    }


    [Fact]
    public void SingleSourceLineage_ShouldBePreserved()
    {
        var source = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "derived-concept.png",
            ArtifactType.ConceptImage,
            new[] { source });


        Assert.Single(artifact.SourceArtifactIds);
        Assert.Equal(source, artifact.SourceArtifactIds[0]);
    }


    [Fact]
    public void MultiSourceLineage_ShouldBePreservedInOrder()
    {
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var sourceC = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "combined-result.png",
            ArtifactType.ConceptImage,
            new[] { sourceA, sourceB, sourceC });


        Assert.Equal(3, artifact.SourceArtifactIds.Count);
        Assert.Equal(sourceA, artifact.SourceArtifactIds[0]);
        Assert.Equal(sourceB, artifact.SourceArtifactIds[1]);
        Assert.Equal(sourceC, artifact.SourceArtifactIds[2]);
    }


    [Fact]
    public void SourceExecutionAndLineageTogether_ShouldBeValid()
    {
        var executionId = WorkflowExecutionId.New();
        var source = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "processed-texture.png",
            ArtifactType.Texture,
            new[] { source },
            executionId);


        Assert.Equal(executionId, artifact.SourceExecutionId);
        Assert.Single(artifact.SourceArtifactIds);
    }


    [Fact]
    public void LineageWithoutSourceExecution_ShouldBeValid()
    {
        var source = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "imported-derivative.png",
            ArtifactType.ConceptImage,
            new[] { source });


        Assert.Null(artifact.SourceExecutionId);
        Assert.Single(artifact.SourceArtifactIds);
    }


    [Fact]
    public void NullSourceCollection_ShouldBeRejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new Artifact(
                ArtifactId.New(),
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                null!));
    }


    [Fact]
    public void EmptySourceArtifactId_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                ArtifactId.New(),
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                new[] { new ArtifactId(Guid.Empty) }));
    }


    [Fact]
    public void DuplicateSourceArtifactIds_ShouldBeRejected()
    {
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();

        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                ArtifactId.New(),
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                new[] { sourceA, sourceB, sourceA }));
    }


    [Fact]
    public void DirectSelfReference_ShouldBeRejected()
    {
        var id = ArtifactId.New();

        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                id,
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                new[] { id }));
    }


    [Fact]
    public void CallerOwnedCollectionMutation_ShouldNotAffectArtifact()
    {
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var sources = new List<ArtifactId> { sourceA, sourceB };

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "name.png",
            ArtifactType.ConceptImage,
            sources);


        sources.Add(ArtifactId.New());


        Assert.Equal(2, artifact.SourceArtifactIds.Count);
        Assert.Equal(sourceA, artifact.SourceArtifactIds[0]);
        Assert.Equal(sourceB, artifact.SourceArtifactIds[1]);
    }


    [Fact]
    public void ExposedSourceCollection_CannotMutateArtifactState()
    {
        var source = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "name.png",
            ArtifactType.ConceptImage,
            new[] { source });


        var exposed = artifact.SourceArtifactIds;

        Assert.IsNotType<List<ArtifactId>>(exposed);

        var act = () => ((ICollection<ArtifactId>)exposed).Add(ArtifactId.New());

        Assert.Throws<NotSupportedException>(act);
    }


    [Fact]
    public void SourceOrder_ShouldBePreservedExactly()
    {
        var sourceC = ArtifactId.New();
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            "name.png",
            ArtifactType.ConceptImage,
            new[] { sourceC, sourceA, sourceB });


        Assert.Equal(sourceC, artifact.SourceArtifactIds[0]);
        Assert.Equal(sourceA, artifact.SourceArtifactIds[1]);
        Assert.Equal(sourceB, artifact.SourceArtifactIds[2]);
    }


    [Fact]
    public void EmptyArtifactId_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                new ArtifactId(Guid.Empty),
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }


    [Fact]
    public void EmptyAssetId_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                ArtifactId.New(),
                new AssetId(Guid.Empty),
                "name.png",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankName_ShouldBeRejected(string? name)
    {
        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                ArtifactId.New(),
                AssetId.New(),
                name!,
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }


    [Fact]
    public void Name_ShouldBePreservedExactly()
    {
        const string name = "Ancient  Gate Texture";

        var artifact = new Artifact(
            ArtifactId.New(),
            AssetId.New(),
            name,
            ArtifactType.Texture,
            Array.Empty<ArtifactId>());


        Assert.Equal(name, artifact.Name);
    }


    [Fact]
    public void EmptySourceExecutionId_WhenPresent_ShouldBeRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new Artifact(
                ArtifactId.New(),
                AssetId.New(),
                "name.png",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>(),
                new WorkflowExecutionId(Guid.Empty)));
    }
}
