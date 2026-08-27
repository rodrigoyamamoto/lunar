using Lunar.Application.Errors;
using Lunar.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Workflows;

public sealed class StartWorkflowExecutionService
{
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;
    private readonly ILogger<StartWorkflowExecutionService> _logger;

    public StartWorkflowExecutionService(
        IWorkflowExecutionRepository workflowExecutionRepository,
        ILogger<StartWorkflowExecutionService> logger)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _workflowExecutionRepository = workflowExecutionRepository;
        _logger = logger;
    }


    public async Task<Result<WorkflowExecution>> StartAsync(
        WorkflowExecutionId workflowExecutionId,
        long expectedRevision,
        CancellationToken cancellationToken = default)
    {
        if (expectedRevision < 0)
        {
            throw new ArgumentException(
                "Expected revision cannot be negative.",
                nameof(expectedRevision));
        }

        var execution = await _workflowExecutionRepository.GetAsync(
            workflowExecutionId,
            cancellationToken);

        if (execution is null)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowExecutionNotFound(workflowExecutionId));
        }

        if (execution.Revision != expectedRevision)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowExecutionConcurrencyConflict(
                    workflowExecutionId,
                    expectedRevision));
        }

        var statusBefore = execution.Status;

        execution.Start();

        if (execution.Status == statusBefore)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowExecutionCannotStart(
                    workflowExecutionId,
                    statusBefore));
        }

        var persisted = await _workflowExecutionRepository.TryUpdateAsync(
            execution,
            expectedRevision,
            cancellationToken);

        if (persisted is null)
        {
            return Result<WorkflowExecution>.Failure(
                new WorkflowExecutionConcurrencyConflict(
                    workflowExecutionId,
                    expectedRevision));
        }

        _logger.LogDebug(
            "Workflow execution started. WorkflowExecutionId={WorkflowExecutionId}",
            workflowExecutionId.Value);

        return Result<WorkflowExecution>.Success(persisted);
    }
}
