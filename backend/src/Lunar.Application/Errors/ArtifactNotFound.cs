using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record ArtifactNotFound(ArtifactId ArtifactId) : ApplicationError(
    $"Artifact not found for {ArtifactId}.");
