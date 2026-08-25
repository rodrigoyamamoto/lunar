using Lunar.Core.Artifacts;

namespace Lunar.Application.Errors;

public sealed record ArtifactWorkflowProvenanceMissing(
    ArtifactId ArtifactId) : ApplicationError(
    $"Artifact {ArtifactId} cannot be recorded as workflow output because it has no SourceExecutionId.");
