using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record ArtifactPersistenceFailed(
    ArtifactId ArtifactId) : ApplicationError(
    $"Artifact {ArtifactId} could not be persisted.");
