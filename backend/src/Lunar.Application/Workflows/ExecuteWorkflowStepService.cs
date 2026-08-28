using System.Diagnostics;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Workflows;

public sealed class ExecuteWorkflowStepService
{
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ICapabilityExecutorResolver _capabilityExecutorResolver;
    private readonly IArtifactContentStore _artifactContentStore;
    private readonly ILogger<ExecuteWorkflowStepService> _logger;

    public ExecuteWorkflowStepService(
        IWorkflowExecutionRepository workflowExecutionRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IArtifactRepository artifactRepository,
        ICapabilityExecutorResolver capabilityExecutorResolver,
        IArtifactContentStore artifactContentStore,
        ILogger<ExecuteWorkflowStepService> logger)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(capabilityExecutorResolver);
        ArgumentNullException.ThrowIfNull(artifactContentStore);
        ArgumentNullException.ThrowIfNull(logger);

        _workflowExecutionRepository = workflowExecutionRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _artifactRepository = artifactRepository;
        _capabilityExecutorResolver = capabilityExecutorResolver;
        _artifactContentStore = artifactContentStore;
        _logger = logger;
    }


    public async Task<Result<ProducedArtifact>> ExecuteAsync(
        WorkflowExecutionId workflowExecutionId,
        int stepPosition,
        CapabilityExecutionInput input,
        WorkflowStepArtifactContext artifactContext,
        CancellationToken cancellationToken = default)
    {
        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(artifactContext);

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

        var executor = _capabilityExecutorResolver.Resolve(step.CapabilityId);

        if (executor is null)
        {
            return Result<ProducedArtifact>.Failure(
                new CapabilityExecutorNotFound(step.CapabilityId));
        }

        CapabilityExecutionOutcome outcome;

        using (var capabilityActivity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.CapabilityExecuteActivityName))
        {
            if (capabilityActivity is not null)
            {
                capabilityActivity.SetTag(ApplicationTelemetry.CapabilityIdTag, step.CapabilityId.Value.ToString());
                capabilityActivity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, workflowExecutionId.Value.ToString());
                capabilityActivity.SetTag(ApplicationTelemetry.WorkflowStepPositionTag, stepPosition);
            }

            var capabilityStopwatch = Stopwatch.StartNew();

            outcome = await executor.ExecuteAsync(
                request,
                cancellationToken);

            capabilityStopwatch.Stop();

            ApplicationTelemetry.CapabilityExecutionDuration.Record(
                capabilityStopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag,
                    outcome is CapabilityExecutionSucceeded
                        ? ApplicationTelemetry.OutcomeSuccess
                        : ApplicationTelemetry.OutcomeFailure));

            if (capabilityActivity is not null)
            {
                if (outcome is CapabilityExecutionSucceeded)
                {
                    capabilityActivity.SetStatus(ActivityStatusCode.Ok);
                }
                else if (outcome is CapabilityExecutionFailed failed)
                {
                    capabilityActivity.SetTag(ApplicationTelemetry.FailureKindTag, failed.Failure.Kind.ToString());
                    capabilityActivity.SetStatus(ActivityStatusCode.Error);
                }
            }
        }

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
                        artifactContext.ArtifactName,
                        artifactContext.ArtifactType,
                        artifactContext.SourceArtifactIds,
                        execution.Id);

                    var contentSizeBytes = output.Content is BinaryArtifactContent binary
                        ? binary.Data.Length
                        : 0;

                    var contentStopwatch = Stopwatch.StartNew();

                    bool contentAdded;

                    using (var contentActivity = ApplicationTelemetry.ActivitySource.StartActivity(
                        ApplicationTelemetry.ArtifactContentPersistActivityName))
                    {
                        if (contentActivity is not null)
                        {
                            contentActivity.SetTag(ApplicationTelemetry.ArtifactIdTag, artifact.Id.Value.ToString());
                        }

                        contentAdded = await _artifactContentStore.TryAddAsync(
                            artifact.Id,
                            output.Content,
                            cancellationToken);

                        if (!contentAdded && contentActivity is not null)
                        {
                            contentActivity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                            contentActivity.SetStatus(ActivityStatusCode.Error);
                        }
                        else if (contentAdded && contentActivity is not null)
                        {
                            contentActivity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
                            contentActivity.SetStatus(ActivityStatusCode.Ok);
                        }
                    }

                    contentStopwatch.Stop();

                    ApplicationTelemetry.ArtifactContentPersistenceDuration.Record(
                        contentStopwatch.Elapsed.TotalMilliseconds,
                        new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag,
                            contentAdded ? ApplicationTelemetry.OutcomeSuccess : ApplicationTelemetry.OutcomeFailure));

                    if (!contentAdded)
                    {
                        return Result<ProducedArtifact>.Failure(
                            new ArtifactContentPersistenceFailed(artifact.Id));
                    }

                    var metadataPersisted = false;

                    try
                    {
                        using var metadataActivity = ApplicationTelemetry.ActivitySource.StartActivity(
                            ApplicationTelemetry.ArtifactMetadataPersistActivityName);

                        if (metadataActivity is not null)
                        {
                            metadataActivity.SetTag(ApplicationTelemetry.ArtifactIdTag, artifact.Id.Value.ToString());
                        }

                        var persisted = await _artifactRepository.TryAddAsync(
                            artifact,
                            cancellationToken);

                        metadataPersisted = persisted;

                        if (!metadataPersisted)
                        {
                            if (metadataActivity is not null)
                            {
                                metadataActivity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                                metadataActivity.SetStatus(ActivityStatusCode.Error);
                            }

                            return Result<ProducedArtifact>.Failure(
                                new ArtifactPersistenceFailed(artifact.Id));
                        }

                        if (metadataActivity is not null)
                        {
                            metadataActivity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
                            metadataActivity.SetStatus(ActivityStatusCode.Ok);
                        }

                        return Result<ProducedArtifact>.Success(
                            new ProducedArtifact(artifact, output.Content));
                    }
                    finally
                    {
                        if (!metadataPersisted)
                        {
                            _logger.LogWarning(
                                "Artifact metadata persistence failed; compensation deleting content. ArtifactId={ArtifactId}",
                                artifact.Id.Value);

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
