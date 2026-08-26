using System.Collections.ObjectModel;
using Lunar.Core.Artifacts;

namespace Lunar.Core.Capabilities;

public sealed class CapabilityExecutionOutput
{
    private readonly ReadOnlyCollection<ArtifactId> _sourceArtifactIds;

    public string ArtifactName { get; }

    public ArtifactType ArtifactType { get; }

    public IReadOnlyList<ArtifactId> SourceArtifactIds => _sourceArtifactIds;

    public ArtifactContent Content { get; }


    public CapabilityExecutionOutput(
        string artifactName,
        ArtifactType artifactType,
        IEnumerable<ArtifactId> sourceArtifactIds,
        ArtifactContent content)
    {
        ArgumentNullException.ThrowIfNull(artifactName);
        ArgumentNullException.ThrowIfNull(sourceArtifactIds);
        ArgumentNullException.ThrowIfNull(content);

        _sourceArtifactIds = sourceArtifactIds.ToList().AsReadOnly();

        ArtifactName = artifactName;
        ArtifactType = artifactType;
        Content = content;
    }
}
