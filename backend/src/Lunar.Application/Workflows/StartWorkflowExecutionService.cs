using Lunar.Application.Errors;
using Lunar.Core.Workflows;

namespace Lunar.Application.Workflows;

public sealed class StartWorkflowExecutionService
{
    private readonly IWorkflowExecutionRepository _workflowExecutionRepository;

    public StartWorkflowExecutionService(
        IWorkflowExecutionRepository workflowExecutionRepository)
    {
        ArgumentNullException.ThrowIfNull(workflowExecutionRepository);

        _workflowExecutionRepository = workflowExecutionRepository;
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

        return Result<WorkflowExecution>.Success(persisted);
    }
}
