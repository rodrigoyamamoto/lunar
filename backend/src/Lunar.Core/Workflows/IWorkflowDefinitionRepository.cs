namespace Lunar.Core.Workflows;

public interface IWorkflowDefinitionRepository
{
    Task<bool> TryAddAsync(
        WorkflowDefinition definition,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinition?> GetAsync(
        WorkflowDefinitionId id,
        int version,
        CancellationToken cancellationToken = default);
}
