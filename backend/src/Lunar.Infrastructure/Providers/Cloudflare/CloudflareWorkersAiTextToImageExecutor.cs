using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareWorkersAiTextToImageExecutor : ICapabilityExecutor
{
    private const string DefaultArtifactName = "Generated image";
    private const string OutputMediaType = "image/jpeg";

    private readonly CloudflareWorkersAiClient _client;

    public CloudflareWorkersAiTextToImageExecutor(CloudflareWorkersAiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
    }


    public async Task<CapabilityExecutionOutcome> ExecuteAsync(
        CapabilityExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Input is not TextPromptInput textPromptInput)
        {
            throw new ArgumentException(
                "CloudflareWorkersAiTextToImageExecutor requires TextPromptInput.",
                nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var result = await _client.GenerateImageAsync(
            textPromptInput.Prompt,
            cancellationToken);

        return result switch
        {
            CloudflareImageGenerationSucceeded succeeded =>
                new CapabilityExecutionSucceeded(
                    new CapabilityExecutionOutput(
                        DefaultArtifactName,
                        ArtifactType.ConceptImage,
                        [],
                        new BinaryArtifactContent(succeeded.ImageBytes, OutputMediaType))),

            CloudflareImageGenerationFailed failed =>
                new CapabilityExecutionFailed(
                    new CapabilityExecutionFailure(
                        failed.Failure.Kind,
                        failed.Failure.RetryAfter)),

            _ => throw new InvalidOperationException(
                "Cloudflare client returned an unsupported result.")
        };
    }
}
