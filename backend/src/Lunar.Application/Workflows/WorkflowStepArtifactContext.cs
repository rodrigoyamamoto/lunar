using System.Collections.ObjectModel;
using Lunar.Core.Artifacts;

namespace Lunar.Application.Workflows;

/// <summary>
/// Application-owned Artifact metadata for a workflow step execution.
/// The provider executor only produces physical content; the Application
/// determines the resulting Artifact's name, type, and direct lineage.
///
/// This is the single authority for Artifact business metadata in the
/// workflow execution path. Provider output (<see cref="Lunar.Core.Capabilities.CapabilityExecutionOutput"/>)
/// carries only content.
///
/// The context defensively snapshots <see cref="SourceArtifactIds"/>
/// at construction time so that caller-side mutation cannot alter
/// provenance after the context is created.
/// </summary>
public sealed record WorkflowStepArtifactContext
{
    private readonly ReadOnlyCollection<ArtifactId> _sourceArtifactIds;

    public string ArtifactName { get; }

    public ArtifactType ArtifactType { get; }

    public IReadOnlyList<ArtifactId> SourceArtifactIds => _sourceArtifactIds;


    public WorkflowStepArtifactContext(
        string artifactName,
        ArtifactType artifactType,
        IReadOnlyList<ArtifactId> sourceArtifactIds)
    {
        if (string.IsNullOrWhiteSpace(artifactName))
        {
            throw new ArgumentException(
                "Artifact name cannot be null, empty, or whitespace.",
                nameof(artifactName));
        }

        ArgumentNullException.ThrowIfNull(sourceArtifactIds);

        ArtifactName = artifactName;
        ArtifactType = artifactType;
        _sourceArtifactIds = Array.AsReadOnly(sourceArtifactIds.ToArray());
    }
}
