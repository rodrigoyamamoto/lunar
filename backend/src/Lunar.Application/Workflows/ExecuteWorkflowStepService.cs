using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Application.Workflows;

public sealed class ExecuteWorkflowStepService
{
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ICapabilityExecutor _capabilityExecutor;

    public ExecuteWorkflowStepService(
        IWorkflowExecutionRepository workflowExecutionRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IArtifactRepository artifactRepository,
        ICapabilityExecutor capabilityExecutor)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(capabilityExecutor);

        _workflowExecutionRepository = workflowExecutionRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _artifactRepository = artifactRepository;
        _capabilityExecutor = capabilityExecutor;
    }


    public async Task<Result<Artifact>> ExecuteAsync(
        WorkflowExecutionId workflowExecutionId,
        int stepPosition,
        CapabilityExecutionInput input,
        CancellationToken cancellationToken = default)
    {
        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        ArgumentNullException.ThrowIfNull(input);

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

        var definition = await _workflowDefinitionRepository.GetAsync(
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            cancellationToken);

        if (definition is null)
        {
            return Result<Artifact>.Failure(
                new WorkflowDefinitionNotFound(
                    execution.WorkflowDefinitionId,
                    execution.WorkflowDefinitionVersion));
        }

        var step = definition.Steps.FirstOrDefault(s => s.Position == stepPosition);

        if (step.Position != stepPosition)
        {
            return Result<Artifact>.Failure(
                new WorkflowStepNotFound(
                    definition.Id,
                    definition.Version,
                    stepPosition));
        }

        var request = new CapabilityExecutionRequest(
            step.CapabilityId,
            execution.AssetId,
            execution.Id,
            execution.WorkflowDefinitionId,
            execution.WorkflowDefinitionVersion,
            step.Position,
            input);

        var output = await _capabilityExecutor.ExecuteAsync(
            request,
            cancellationToken);

        if (output is null)
        {
            throw new InvalidOperationException(
                "Capability executor returned null output, violating its contract.");
        }

        var artifact = new Artifact(
            ArtifactId.New(),
            execution.AssetId,
            output.ArtifactName,
            output.ArtifactType,
            output.SourceArtifactIds,
            execution.Id);

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
