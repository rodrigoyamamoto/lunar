using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Application.Workflows;

public sealed class GenerateArtifactService
{
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly CreateWorkflowExecutionService _createWorkflowExecutionService;
    private readonly StartWorkflowExecutionService _startWorkflowExecutionService;
    private readonly ExecuteWorkflowStepService _executeWorkflowStepService;

    public GenerateArtifactService(
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        CreateWorkflowExecutionService createWorkflowExecutionService,
        StartWorkflowExecutionService startWorkflowExecutionService,
        ExecuteWorkflowStepService executeWorkflowStepService)
    {
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(createWorkflowExecutionService);
        ArgumentNullException.ThrowIfNull(startWorkflowExecutionService);
        ArgumentNullException.ThrowIfNull(executeWorkflowStepService);

        _workflowDefinitionRepository = workflowDefinitionRepository;
        _createWorkflowExecutionService = createWorkflowExecutionService;
        _startWorkflowExecutionService = startWorkflowExecutionService;
        _executeWorkflowStepService = executeWorkflowStepService;
    }


    public async Task<Result<GeneratedArtifact>> GenerateAsync(
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        int stepPosition,
        CapabilityExecutionInput input,
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

        var definition = await _workflowDefinitionRepository.GetAsync(
            workflowDefinitionId,
            workflowDefinitionVersion,
            cancellationToken);

        if (definition is null)
        {
            return Result<GeneratedArtifact>.Failure(
                new WorkflowDefinitionNotFound(
                    workflowDefinitionId,
                    workflowDefinitionVersion));
        }

        var step = definition.Steps.FirstOrDefault(s => s.Position == stepPosition);

        if (step.Position != stepPosition)
        {
            return Result<GeneratedArtifact>.Failure(
                new WorkflowStepNotFound(
                    definition.Id,
                    definition.Version,
                    stepPosition));
        }

        var createResult = await _createWorkflowExecutionService.CreateAsync(
            assetId,
            workflowDefinitionId,
            workflowDefinitionVersion,
            cancellationToken);

        if (createResult.IsFailure)
        {
            return Result<GeneratedArtifact>.Failure(createResult.Error!);
        }

        var execution = createResult.Value!;

        var startResult = await _startWorkflowExecutionService.StartAsync(
            execution.Id,
            expectedRevision: 0,
            cancellationToken);

        if (startResult.IsFailure)
        {
            return Result<GeneratedArtifact>.Failure(startResult.Error!);
        }

        var executeResult = await _executeWorkflowStepService.ExecuteAsync(
            execution.Id,
            stepPosition,
            input,
            cancellationToken);

        if (executeResult.IsFailure)
        {
            return Result<GeneratedArtifact>.Failure(executeResult.Error!);
        }

        return Result<GeneratedArtifact>.Success(
            new GeneratedArtifact(
                execution.Id,
                executeResult.Value!));
    }
}
