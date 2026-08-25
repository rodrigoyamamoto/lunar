using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowExecutionCannotStart(
    WorkflowExecutionId WorkflowExecutionId,
    WorkflowExecutionStatus CurrentStatus) : ApplicationError(
    $"Workflow execution {WorkflowExecutionId} cannot start from {CurrentStatus}.");
