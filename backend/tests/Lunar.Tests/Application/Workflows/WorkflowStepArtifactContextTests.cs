using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;

namespace Lunar.Tests.Application.Workflows;

public class WorkflowStepArtifactContextTests
{
    [Fact]
    public void Constructor_ValidValues_PreservesNameTypeAndSourceIds()
    {
        var sourceIds = new[] { ArtifactId.New(), ArtifactId.New() };

        var context = new WorkflowStepArtifactContext(
            "Derived Artifact",
            ArtifactType.Texture,
            sourceIds);

        Assert.Equal("Derived Artifact", context.ArtifactName);
        Assert.Equal(ArtifactType.Texture, context.ArtifactType);
        Assert.Equal(2, context.SourceArtifactIds.Count);
        Assert.Equal(sourceIds[0], context.SourceArtifactIds[0]);
        Assert.Equal(sourceIds[1], context.SourceArtifactIds[1]);
    }

    [Fact]
    public void Constructor_NullArtifactName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStepArtifactContext(
                null!,
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }

    [Fact]
    public void Constructor_EmptyArtifactName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStepArtifactContext(
                "",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }

    [Fact]
    public void Constructor_WhitespaceArtifactName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStepArtifactContext(
                "   ",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>()));
    }

    [Fact]
    public void Constructor_NullSourceArtifactIds_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowStepArtifactContext(
                "Test",
                ArtifactType.ConceptImage,
                null!));
    }

    [Fact]
    public void Constructor_CallerCollectionMutation_DoesNotAlterContext()
    {
        var sourceIds = new List<ArtifactId> { ArtifactId.New() };
        var context = new WorkflowStepArtifactContext(
            "Test",
            ArtifactType.ConceptImage,
            sourceIds);

        // Mutate the caller's list after construction
        sourceIds.Add(ArtifactId.New());
        sourceIds.Clear();

        // Context must reflect the original snapshot
        Assert.Single(context.SourceArtifactIds);
    }

    [Fact]
    public void Constructor_SourceOrderPreserved()
    {
        var first = ArtifactId.New();
        var second = ArtifactId.New();
        var third = ArtifactId.New();

        var context = new WorkflowStepArtifactContext(
            "Test",
            ArtifactType.Texture,
            new[] { first, second, third });

        Assert.Equal(3, context.SourceArtifactIds.Count);
        Assert.Equal(first, context.SourceArtifactIds[0]);
        Assert.Equal(second, context.SourceArtifactIds[1]);
        Assert.Equal(third, context.SourceArtifactIds[2]);
    }

    [Fact]
    public void SourceArtifactIds_CannotBeMutatedByCastingToArray()
    {
        var sourceId = ArtifactId.New();
        var context = new WorkflowStepArtifactContext(
            "Test",
            ArtifactType.ConceptImage,
            new[] { sourceId });

        // A consumer must not be able to cast the exposed collection
        // back to a mutable array and alter the authoritative snapshot.
        Assert.Throws<InvalidCastException>(() =>
        {
            var asArray = (ArtifactId[])context.SourceArtifactIds;
            asArray[0] = ArtifactId.New();
        });

        // Context still contains exactly the original source ID
        Assert.Single(context.SourceArtifactIds);
        Assert.Equal(sourceId, context.SourceArtifactIds[0]);
    }

    [Fact]
    public void SourceArtifactIds_CannotBeMutatedThroughMutableInterface()
    {
        var sourceId = ArtifactId.New();
        var context = new WorkflowStepArtifactContext(
            "Test",
            ArtifactType.ConceptImage,
            new[] { sourceId });

        // The exposed collection must not implement a mutable interface
        // that allows modification.
        Assert.Throws<InvalidCastException>(() =>
        {
            var asList = (System.Collections.Generic.List<ArtifactId>)context.SourceArtifactIds;
            asList.Add(ArtifactId.New());
        });

        Assert.Throws<InvalidCastException>(() =>
        {
            var asCollection = (System.Collections.ObjectModel.Collection<ArtifactId>)context.SourceArtifactIds;
            asCollection.Add(ArtifactId.New());
        });

        Assert.Single(context.SourceArtifactIds);
        Assert.Equal(sourceId, context.SourceArtifactIds[0]);
    }

    [Fact]
    public void SourceArtifactIds_IListMutationThrowsNotSupportedException()
    {
        var original = ArtifactId.New();
        var context = new WorkflowStepArtifactContext(
            "Test",
            ArtifactType.ConceptImage,
            new[] { original });

        // ReadOnlyCollection<T> implements IList<T> but mutation
        // operations throw NotSupportedException. This is the real
        // mutable interface exposed by the collection type.
        var list = (IList<ArtifactId>)context.SourceArtifactIds;

        Assert.Throws<NotSupportedException>(() =>
            list[0] = ArtifactId.New());

        Assert.Throws<NotSupportedException>(() =>
            list.Add(ArtifactId.New()));

        Assert.Throws<NotSupportedException>(() =>
            list.Insert(0, ArtifactId.New()));

        Assert.Throws<NotSupportedException>(() =>
            list.Clear());

        Assert.Throws<NotSupportedException>(() =>
            list.RemoveAt(0));

        // Context still contains exactly the original source ID
        Assert.Single(context.SourceArtifactIds);
        Assert.Equal(original, context.SourceArtifactIds[0]);
    }
}
