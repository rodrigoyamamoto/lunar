using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record ArtifactContentPersistenceFailed(
    ArtifactId ArtifactId) : ApplicationError(
    $"Artifact content could not be persisted for {ArtifactId}.");
