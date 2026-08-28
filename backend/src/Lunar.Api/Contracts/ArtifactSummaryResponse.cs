namespace Lunar.Api.Contracts;

public sealed class ArtifactSummaryResponse
{
    public Guid ArtifactId { get; init; }

    public Guid AssetId { get; init; }

    public required string ArtifactName { get; init; }

    public required string ArtifactType { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required string ContentUrl { get; init; }

    public GenerationInputResponse? GenerationInput { get; init; }
}
