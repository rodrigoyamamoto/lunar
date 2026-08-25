using Lunar.Core.Artifacts;
using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record ArtifactWorkflowExecutionMismatch(
    ArtifactId ArtifactId,
    WorkflowExecutionId RequestedWorkflowExecutionId,
    WorkflowExecutionId ArtifactSourceExecutionId) : ApplicationError(
    $"Artifact {ArtifactId} SourceExecutionId {ArtifactSourceExecutionId} does not match requested workflow execution {RequestedWorkflowExecutionId}.");
