using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowExecutionNotFound(
    WorkflowExecutionId WorkflowExecutionId) : ApplicationError(
    $"Workflow execution {WorkflowExecutionId} was not found.");
