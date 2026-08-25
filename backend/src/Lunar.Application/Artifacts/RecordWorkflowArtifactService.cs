using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Workflows;

namespace Lunar.Application.Artifacts;

public sealed class RecordWorkflowArtifactService
{
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;
    private readonly IArtifactRepository _artifactRepository;

    public RecordWorkflowArtifactService(
        IWorkflowExecutionRepository workflowExecutionRepository,
        IArtifactRepository artifactRepository)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);

        _workflowExecutionRepository = workflowExecutionRepository;
        _artifactRepository = artifactRepository;
    }


    public async Task<Result<Artifact>> RecordAsync(
        WorkflowExecutionId workflowExecutionId,
        Artifact artifact,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);

        var execution = await _workflowExecutionRepository.GetAsync(
            workflowExecutionId,
            cancellationToken);

        if (execution is null)
        {
            return Result<Artifact>.Failure(
                new WorkflowExecutionNotFound(workflowExecutionId));
        }

        if (execution.Status != WorkflowExecutionStatus.Running)
        {
            return Result<Artifact>.Failure(
                new WorkflowExecutionNotRunning(
                    workflowExecutionId,
                    execution.Status));
        }

        if (artifact.SourceExecutionId is null)
        {
            return Result<Artifact>.Failure(
                new ArtifactWorkflowProvenanceMissing(artifact.Id));
        }

        if (artifact.SourceExecutionId != workflowExecutionId)
        {
            return Result<Artifact>.Failure(
                new ArtifactWorkflowExecutionMismatch(
                    artifact.Id,
                    workflowExecutionId,
                    artifact.SourceExecutionId.Value));
        }

        if (artifact.AssetId != execution.AssetId)
        {
            return Result<Artifact>.Failure(
                new ArtifactWorkflowAssetMismatch(
                    artifact.Id,
                    workflowExecutionId,
                    execution.AssetId,
                    artifact.AssetId));
        }

        var persisted = await _artifactRepository.TryAddAsync(
            artifact,
            cancellationToken);

        if (!persisted)
        {
            return Result<Artifact>.Failure(
                new ArtifactPersistenceFailed(artifact.Id));
        }

        return Result<Artifact>.Success(artifact);
    }
}
