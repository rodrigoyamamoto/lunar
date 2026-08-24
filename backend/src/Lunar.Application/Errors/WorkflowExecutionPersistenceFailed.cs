using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowExecutionPersistenceFailed(
    WorkflowExecutionId WorkflowExecutionId) : ApplicationError(
    $"Workflow execution {WorkflowExecutionId} could not be persisted.");
