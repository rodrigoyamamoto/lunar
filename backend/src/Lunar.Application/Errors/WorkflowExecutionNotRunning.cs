using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowExecutionNotRunning(
    WorkflowExecutionId WorkflowExecutionId,
    WorkflowExecutionStatus CurrentStatus) : ApplicationError(
    $"Workflow execution {WorkflowExecutionId} is not Running (current status: {CurrentStatus}).");
