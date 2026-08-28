namespace Lunar.Api.Contracts;

public sealed class GenerationInputResponse
{
    public Guid WorkflowExecutionId { get; init; }

    public required string Prompt { get; init; }
}
