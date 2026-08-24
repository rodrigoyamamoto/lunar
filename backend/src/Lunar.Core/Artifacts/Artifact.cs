using System.Collections.ObjectModel;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Core.Artifacts;

public sealed class Artifact
{
    private readonly ReadOnlyCollection<ArtifactId> _sourceArtifactIds;

    public ArtifactId Id { get; }

    public AssetId AssetId { get; }

    public string Name { get; }

    public ArtifactType Type { get; }

    public WorkflowExecutionId? SourceExecutionId { get; }

    public IReadOnlyList<ArtifactId> SourceArtifactIds => _sourceArtifactIds;

    public DateTimeOffset CreatedAt { get; }


    public Artifact(
        ArtifactId id,
        AssetId assetId,
        string name,
        ArtifactType type,
        IEnumerable<ArtifactId> sourceArtifactIds,
        WorkflowExecutionId? sourceExecutionId = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(id));
        }

        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Artifact name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        if (sourceExecutionId.HasValue && sourceExecutionId.Value.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Source execution identifier cannot be empty when present.",
                nameof(sourceExecutionId));
        }

        ArgumentNullException.ThrowIfNull(sourceArtifactIds);

        var sourceList = sourceArtifactIds.ToList();

        ValidateSourceArtifactIds(sourceList, id);

        _sourceArtifactIds = sourceList.AsReadOnly();

        Id = id;
        AssetId = assetId;
        Name = name;
        Type = type;
        SourceExecutionId = sourceExecutionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }


    private static void ValidateSourceArtifactIds(List<ArtifactId> sourceIds, ArtifactId ownId)
    {
        var seen = new HashSet<ArtifactId>();

        for (var i = 0; i < sourceIds.Count; i++)
        {
            var sourceId = sourceIds[i];

            if (sourceId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Source artifact identifiers cannot be empty.",
                    "sourceArtifactIds");
            }

            if (sourceId == ownId)
            {
                throw new ArgumentException(
                    "An artifact cannot list itself as a direct source.",
                    "sourceArtifactIds");
            }

            if (!seen.Add(sourceId))
            {
                throw new ArgumentException(
                    "Source artifact identifiers cannot contain duplicates.",
                    "sourceArtifactIds");
            }
        }
    }
}
