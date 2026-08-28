namespace Lunar.Api.Contracts;

public sealed class ArtifactTransformationResponse
{
    public Guid WorkflowExecutionId { get; init; }

    public Guid ArtifactId { get; init; }

    public Guid AssetId { get; init; }

    public required string ArtifactName { get; init; }

    public required string ArtifactType { get; init; }

    public required string MediaType { get; init; }

    public required string ContentUrl { get; init; }

    public required IReadOnlyList<Guid> SourceArtifactIds { get; init; }
}
