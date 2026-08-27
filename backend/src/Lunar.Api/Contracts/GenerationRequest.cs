namespace Lunar.Api.Contracts;

public sealed class GenerationRequest
{
    public Guid AssetId { get; init; }

    public Guid WorkflowDefinitionId { get; init; }

    public int WorkflowDefinitionVersion { get; init; }

    public int StepPosition { get; init; }

    public required string Prompt { get; init; }
}
