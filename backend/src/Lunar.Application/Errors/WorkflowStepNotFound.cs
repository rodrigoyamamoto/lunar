using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowStepNotFound(
    WorkflowDefinitionId WorkflowDefinitionId,
    int WorkflowDefinitionVersion,
    int StepPosition) : ApplicationError(
    $"Workflow step not found at position {StepPosition} in definition {WorkflowDefinitionId} version {WorkflowDefinitionVersion}.");
