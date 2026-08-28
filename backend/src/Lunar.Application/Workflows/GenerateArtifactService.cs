using System.Diagnostics;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Workflows;

public sealed class GenerateArtifactService
{
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly CreateWorkflowExecutionService _createWorkflowExecutionService;
    private readonly StartWorkflowExecutionService _startWorkflowExecutionService;
    private readonly ExecuteWorkflowStepService _executeWorkflowStepService;
    private readonly IGenerationInputRecordRepository _generationInputRecordRepository;
    private readonly ILogger<GenerateArtifactService> _logger;

    public GenerateArtifactService(
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        CreateWorkflowExecutionService createWorkflowExecutionService,
        StartWorkflowExecutionService startWorkflowExecutionService,
        ExecuteWorkflowStepService executeWorkflowStepService,
        IGenerationInputRecordRepository generationInputRecordRepository,
        ILogger<GenerateArtifactService> logger)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(createWorkflowExecutionService);
        ArgumentNullException.ThrowIfNull(startWorkflowExecutionService);
        ArgumentNullException.ThrowIfNull(executeWorkflowStepService);
        ArgumentNullException.ThrowIfNull(generationInputRecordRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _workflowDefinitionRepository = workflowDefinitionRepository;
        _createWorkflowExecutionService = createWorkflowExecutionService;
        _startWorkflowExecutionService = startWorkflowExecutionService;
        _executeWorkflowStepService = executeWorkflowStepService;
        _generationInputRecordRepository = generationInputRecordRepository;
        _logger = logger;
    }


    public async Task<Result<GeneratedArtifact>> GenerateAsync(
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        int stepPosition,
        CapabilityExecutionInput input,
        WorkflowStepArtifactContext artifactContext,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        if (workflowDefinitionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow definition identifier cannot be empty.",
                nameof(workflowDefinitionId));
        }

        if (workflowDefinitionVersion < 1)
        {
            throw new ArgumentException(
                "Workflow definition version must be a positive integer.",
                nameof(workflowDefinitionVersion));
        }

        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(artifactContext);

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.WorkflowGenerateActivityName);

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.AssetIdTag, assetId.Value.ToString());
            activity.SetTag(ApplicationTelemetry.WorkflowDefinitionIdTag, workflowDefinitionId.Value.ToString());
            activity.SetTag(ApplicationTelemetry.WorkflowDefinitionVersionTag, workflowDefinitionVersion);
            activity.SetTag(ApplicationTelemetry.WorkflowStepPositionTag, stepPosition);
        }

        var definition = await _workflowDefinitionRepository.GetAsync(
            workflowDefinitionId,
            workflowDefinitionVersion,
            cancellationToken);

        if (definition is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageWorkflowPrevalidation);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new WorkflowDefinitionNotFound(
                    workflowDefinitionId,
                    workflowDefinitionVersion));
        }

        var step = definition.Steps.FirstOrDefault(s => s.Position == stepPosition);

        if (step.Position != stepPosition)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageWorkflowPrevalidation);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new WorkflowStepNotFound(
                    definition.Id,
                    definition.Version,
                    stepPosition));
        }

        WorkflowExecutionId executionId;

        using (var createActivity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.WorkflowExecutionCreateActivityName))
        {
            var createResult = await _createWorkflowExecutionService.CreateAsync(
                assetId,
                workflowDefinitionId,
                workflowDefinitionVersion,
                cancellationToken);

            if (createResult.IsFailure)
            {
                if (activity is not null)
                {
                    activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                    activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageWorkflowExecutionCreation);
                    activity.SetStatus(ActivityStatusCode.Error);
                }

                return Result<GeneratedArtifact>.Failure(createResult.Error!);
            }

            executionId = createResult.Value!.Id;

            if (createActivity is not null)
            {
                createActivity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, executionId.Value.ToString());
            }
        }

        if (input is TextPromptInput textPromptInput)
        {
            var inputRecord = new GenerationInputRecord(
                executionId,
                assetId,
                textPromptInput);

            var inputPersisted = await _generationInputRecordRepository.TryAddAsync(
                inputRecord,
                cancellationToken);

            if (!inputPersisted)
            {
                if (activity is not null)
                {
                    activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                    activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageGenerationInputPersistence);
                    activity.SetStatus(ActivityStatusCode.Error);
                }

                return Result<GeneratedArtifact>.Failure(
                    new GenerationInputPersistenceFailed(executionId));
            }
        }

        using (var startActivity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.WorkflowExecutionStartActivityName))
        {
            if (startActivity is not null)
            {
                startActivity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, executionId.Value.ToString());
            }

            var startResult = await _startWorkflowExecutionService.StartAsync(
                executionId,
                expectedRevision: 0,
                cancellationToken);

            if (startResult.IsFailure)
            {
                if (activity is not null)
                {
                    activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                    activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageWorkflowExecutionStart);
                    activity.SetStatus(ActivityStatusCode.Error);
                }

                return Result<GeneratedArtifact>.Failure(startResult.Error!);
            }
        }

        Result<ProducedArtifact> executeResult;

        using (var stepActivity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.WorkflowStepExecuteActivityName))
        {
            if (stepActivity is not null)
            {
                stepActivity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, executionId.Value.ToString());
                stepActivity.SetTag(ApplicationTelemetry.WorkflowStepPositionTag, stepPosition);
                stepActivity.SetTag(ApplicationTelemetry.CapabilityIdTag, step.CapabilityId.Value.ToString());
            }

            executeResult = await _executeWorkflowStepService.ExecuteAsync(
                executionId,
                stepPosition,
                input,
                artifactContext,
                cancellationToken);
        }

        if (executeResult.IsFailure)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(executeResult.Error!);
        }

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, executionId.Value.ToString());
            activity.SetTag(ApplicationTelemetry.ArtifactIdTag, executeResult.Value!.Artifact.Id.Value.ToString());
            activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        return Result<GeneratedArtifact>.Success(
            new GeneratedArtifact(
                executionId,
                executeResult.Value!));
    }
}
