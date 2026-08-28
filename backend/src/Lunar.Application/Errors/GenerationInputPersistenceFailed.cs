using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record GenerationInputPersistenceFailed(WorkflowExecutionId WorkflowExecutionId) : ApplicationError(
    $"Generation input persistence failed for {WorkflowExecutionId}.");
