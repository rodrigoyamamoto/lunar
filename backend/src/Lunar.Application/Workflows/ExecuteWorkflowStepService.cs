using Lunar.Application.Artifacts;
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
    private readonly IArtifactContentStore _artifactContentStore;

    public ExecuteWorkflowStepService(
        IWorkflowExecutionRepository workflowExecutionRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IArtifactRepository artifactRepository,
        ICapabilityExecutor capabilityExecutor,
        IArtifactContentStore artifactContentStore)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(capabilityExecutor);
        ArgumentNullException.ThrowIfNull(artifactContentStore);

        _workflowExecutionRepository = workflowExecutionRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _artifactRepository = artifactRepository;
        _capabilityExecutor = capabilityExecutor;
        _artifactContentStore = artifactContentStore;
    }


    public async Task<Result<ProducedArtifact>> ExecuteAsync(
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
            return Result<ProducedArtifact>.Failure(
                new WorkflowExecutionNotFound(workflowExecutionId));
        }

        if (execution.Status != WorkflowExecutionStatus.Running)
        {
            return Result<ProducedArtifact>.Failure(
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
            return Result<ProducedArtifact>.Failure(
                new WorkflowDefinitionNotFound(
                    execution.WorkflowDefinitionId,
                    execution.WorkflowDefinitionVersion));
        }

        var step = definition.Steps.FirstOrDefault(s => s.Position == stepPosition);

        if (step.Position != stepPosition)
        {
            return Result<ProducedArtifact>.Failure(
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

        var outcome = await _capabilityExecutor.ExecuteAsync(
            request,
            cancellationToken);

        if (outcome is null)
        {
            throw new InvalidOperationException(
                "Capability executor returned null in violation of ICapabilityExecutor's contract.");
        }

        switch (outcome)
        {
            case CapabilityExecutionSucceeded succeeded:
                {
                    var output = succeeded.Output;

                    var artifact = new Artifact(
                        ArtifactId.New(),
                        execution.AssetId,
                        output.ArtifactName,
                        output.ArtifactType,
                        output.SourceArtifactIds,
                        execution.Id);

                    var contentAdded = await _artifactContentStore.TryAddAsync(
                        artifact.Id,
                        output.Content,
                        cancellationToken);

                    if (!contentAdded)
                    {
                        return Result<ProducedArtifact>.Failure(
                            new ArtifactContentPersistenceFailed(artifact.Id));
                    }

                    var metadataPersisted = false;

                    try
                    {
                        var persisted = await _artifactRepository.TryAddAsync(
                            artifact,
                            cancellationToken);

                        metadataPersisted = persisted;

                        if (!metadataPersisted)
                        {
                            return Result<ProducedArtifact>.Failure(
                                new ArtifactPersistenceFailed(artifact.Id));
                        }

                        return Result<ProducedArtifact>.Success(
                            new ProducedArtifact(artifact, output.Content));
                    }
                    finally
                    {
                        if (!metadataPersisted)
                        {
                            await _artifactContentStore.TryDeleteAsync(
                                artifact.Id,
                                CancellationToken.None);
                        }
                    }
                }

            case CapabilityExecutionFailed failed:
                return Result<ProducedArtifact>.Failure(
                    new WorkflowStepExecutionFailed(
                        workflowExecutionId,
                        stepPosition,
                        failed.Failure));

            default:
                throw new InvalidOperationException(
                    "Capability executor returned an unsupported outcome.");
        }
    }
}
