using Lunar.Core.Workflows;

namespace Lunar.Application.Errors;

public sealed record WorkflowDefinitionNotFound(
    WorkflowDefinitionId WorkflowDefinitionId,
    int Version) : ApplicationError(
    $"Workflow definition not found for ({WorkflowDefinitionId}, version {Version}).");
