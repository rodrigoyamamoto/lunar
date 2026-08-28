using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record UnsupportedArtifactContent(
    ArtifactId ArtifactId,
    string MediaType) : ApplicationError(
    $"Artifact {ArtifactId} has unsupported content media type '{MediaType}' for this operation.");
