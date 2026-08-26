namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareWorkersAiOptions
{
    public string BaseAddress { get; set; } = string.Empty;

    public string AccountId { get; set; } = string.Empty;

    public string ApiToken { get; set; } = string.Empty;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(1);

    public string TextToImageModelId { get; set; } = "@cf/black-forest-labs/flux-1-schnell";

    public int TextToImageSteps { get; set; } = 4;
}
