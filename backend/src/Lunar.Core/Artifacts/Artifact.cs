using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Core.Artifacts;

public sealed class Artifact
{
    public ArtifactId Id { get; }

    public AssetId AssetId { get; }

    public string Name { get; }

    public ArtifactType Type { get; }

    public WorkflowExecutionId? SourceExecutionId { get; }

    public DateTimeOffset CreatedAt { get; }


    public Artifact(
        ArtifactId id,
        AssetId assetId,
        string name,
        ArtifactType type,
        WorkflowExecutionId? sourceExecutionId = null)
    {
        Id = id;
        AssetId = assetId;
        Name = name;
        Type = type;
        SourceExecutionId = sourceExecutionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
