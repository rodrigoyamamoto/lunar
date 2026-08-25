using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record ArtifactWorkflowAssetMismatch(
    ArtifactId ArtifactId,
    WorkflowExecutionId WorkflowExecutionId,
    AssetId ExecutionAssetId,
    AssetId ArtifactAssetId) : ApplicationError(
    $"Artifact {ArtifactId} belongs to asset {ArtifactAssetId}, but workflow execution {WorkflowExecutionId} is for asset {ExecutionAssetId}.");
