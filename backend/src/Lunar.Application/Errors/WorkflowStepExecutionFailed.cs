using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowStepExecutionFailed : ApplicationError
{
    public WorkflowExecutionId WorkflowExecutionId { get; }

    public int StepPosition { get; }

    public CapabilityExecutionFailure Failure { get; }

    public CapabilityExecutionFailureKind Kind => Failure.Kind;

    public TimeSpan? RetryAfter => Failure.RetryAfter;


    public WorkflowStepExecutionFailed(
        WorkflowExecutionId workflowExecutionId,
        int stepPosition,
        CapabilityExecutionFailure failure)
        : base(BuildMessage(workflowExecutionId, stepPosition, failure))
    {
        WorkflowExecutionId = workflowExecutionId;
        StepPosition = stepPosition;
        Failure = failure;
    }


    private static string BuildMessage(
        WorkflowExecutionId workflowExecutionId,
        int stepPosition,
        CapabilityExecutionFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        if (workflowExecutionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(workflowExecutionId));
        }

        if (stepPosition < 1)
        {
            throw new ArgumentException(
                "Step position must be a positive integer.",
                nameof(stepPosition));
        }

        return $"Workflow step execution failed at position {stepPosition} in execution {workflowExecutionId} with kind {failure.Kind}.";
    }
}
