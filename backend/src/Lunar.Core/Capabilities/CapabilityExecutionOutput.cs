using Lunar.Core.Artifacts;

namespace Lunar.Core.Capabilities;

/// <summary>
/// Core physical output produced by a capability executor. Contains
/// only the content that the capability physically produced.
///
/// Lunar Artifact business metadata — name, type, and direct lineage
/// (<see cref="Artifact.SourceArtifactIds"/>) — is owned by the
/// Application/workflow execution context, not by the provider
/// executor. This prevents a provider from altering Lunar business
/// classification or provenance.
/// </summary>
public sealed class CapabilityExecutionOutput
{
    public ArtifactContent Content { get; }


    public CapabilityExecutionOutput(ArtifactContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
    }
}
