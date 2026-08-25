using Lunar.Application.Errors;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;

namespace Lunar.Application.Workflows;

public sealed class ExecuteWorkflowService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IWorkflowDefinitionRepository _workflowDefinitionRepository;
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;

    public ExecuteWorkflowService(
        IAssetRepository assetRepository,
        IWorkflowDefinitionRepository workflowDefinitionRepository,
        IWorkflowExecutionRepository workflowExecutionRepository)
    {
        ArgumentNullException.ThrowIfNull(assetRepository);
        ArgumentNullException.ThrowIfNull(workflowDefinitionRepository);
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);

        _assetRepository = assetRepository;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowExecutionRepository = workflowExecutionRepository;
    }


    public async Task<Result<WorkflowExecution>> ExecuteAsync(
        AssetId assetId,
        WorkflowDefinitionId workflowDefinitionId,
        int workflowDefinitionVersion,
        CancellationToken cancellationToken = default)
    {
        var asset = await _assetRepository.GetAsync(
            assetId,
            cancellationToken);

        if (asset is null)
        {
            return Result<WorkflowExecution>.Failure(
                new AssetNotFound(assetId));
        }

        var definition = await _workflowDefinitionRepository.GetAsync(
            workflowDefinitionId,
            workflowDefinitionVersion,
            cancellationToken);

        if (definition is null)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowDefinitionNotFound(workflowDefinitionId, workflowDefinitionVersion));
        }

        var execution = WorkflowExecution.Create(
            assetId,
            workflowDefinitionId,
            workflowDefinitionVersion);

        var added = await _workflowExecutionRepository.TryAddAsync(
            execution,
            cancellationToken);

        if (!added)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowExecutionPersistenceFailed(execution.Id));
        }

        return Result<WorkflowExecution>.Success(execution);
    }
}
