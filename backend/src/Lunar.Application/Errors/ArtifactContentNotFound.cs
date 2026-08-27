using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record ArtifactContentNotFound(ArtifactId ArtifactId) : ApplicationError(
    $"Durable artifact content not found for {ArtifactId}.");
