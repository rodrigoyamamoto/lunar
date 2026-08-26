using Lunar.Core.Artifacts;

namespace Lunar.Application.Artifacts;

public sealed record ProducedArtifact
{
    public Artifact Artifact { get; }

    public ArtifactContent Content { get; }


    public ProducedArtifact(Artifact artifact, ArtifactContent content)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(content);

        Artifact = artifact;
        Content = content;
    }
}
