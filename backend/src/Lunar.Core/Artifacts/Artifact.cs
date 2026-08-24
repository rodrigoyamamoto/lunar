using Lunar.Core.Assets;

namespace Lunar.Core.Artifacts;

public sealed class Artifact
{
    public ArtifactId Id { get; }

    public AssetId AssetId { get; }

    public string Name { get; }

    public ArtifactType Type { get; }

    public DateTimeOffset CreatedAt { get; }


    public Artifact(
        ArtifactId id,
        AssetId assetId,
        string name,
        ArtifactType type)
    {
        Id = id;
        AssetId = assetId;
        Name = name;
        Type = type;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}