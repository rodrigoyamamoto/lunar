using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowExecutionConcurrencyConflict(
    WorkflowExecutionId WorkflowExecutionId,
    long ExpectedRevision) : ApplicationError(
    $"Workflow execution {WorkflowExecutionId} could not be updated because expected revision {ExpectedRevision} no longer matches persisted state.");
