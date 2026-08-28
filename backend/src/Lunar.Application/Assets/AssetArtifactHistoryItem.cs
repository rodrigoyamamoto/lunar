using Lunar.Core.Artifacts;
using Lunar.Core.Workflows;

namespace Lunar.Application.Assets;

/// <summary>
/// Application-layer gallery composition pairing an <see cref="Artifact"/>
/// with its optional <see cref="GenerationInputRecord"/> provenance.
/// Provenance is associated via <see cref="Artifact.SourceExecutionId"/>
/// and may be absent for Artifacts without recorded generation history.
/// </summary>
public sealed record AssetArtifactHistoryItem
{
    public Artifact Artifact { get; }

    public GenerationInputRecord? GenerationInput { get; }


    public AssetArtifactHistoryItem(
        Artifact artifact,
        GenerationInputRecord? generationInput)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        Artifact = artifact;
        GenerationInput = generationInput;
    }
}
