namespace Lunar.Api.Contracts;

public sealed class GenerationRequest
{
    public Guid AssetId { get; init; }

    public required string Prompt { get; init; }
}
