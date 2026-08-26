namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareWorkersAiConfiguration
{
    public Uri BaseAddress { get; }

    public string AccountId { get; }

    public string ApiToken { get; }

    public TimeSpan RequestTimeout { get; }

    public string TextToImageModelId { get; }

    public int TextToImageSteps { get; }


    private CloudflareWorkersAiConfiguration(
        Uri baseAddress,
        string accountId,
        string apiToken,
        TimeSpan requestTimeout,
        string textToImageModelId,
        int textToImageSteps)
    {
        BaseAddress = baseAddress;
        AccountId = accountId;
        ApiToken = apiToken;
        RequestTimeout = requestTimeout;
        TextToImageModelId = textToImageModelId;
        TextToImageSteps = textToImageSteps;
    }


    public static CloudflareWorkersAiConfiguration From(CloudflareWorkersAiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.BaseAddress))
        {
            throw new InvalidOperationException(
                "Cloudflare BaseAddress is not configured.");
        }

        if (!Uri.TryCreate(options.BaseAddress, UriKind.Absolute, out var baseAddress))
        {
            throw new InvalidOperationException(
                "Cloudflare BaseAddress must be an absolute URI.");
        }

        if (baseAddress.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "Cloudflare BaseAddress must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(options.AccountId))
        {
            throw new InvalidOperationException(
                "Cloudflare AccountId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiToken))
        {
            throw new InvalidOperationException(
                "Cloudflare ApiToken is not configured.");
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "Cloudflare RequestTimeout must be strictly positive.");
        }

        if (string.IsNullOrWhiteSpace(options.TextToImageModelId))
        {
            throw new InvalidOperationException(
                "Cloudflare TextToImageModelId is not configured.");
        }

        if (options.TextToImageSteps < 1 || options.TextToImageSteps > 8)
        {
            throw new InvalidOperationException(
                "Cloudflare TextToImageSteps must be between 1 and 8.");
        }

        return new CloudflareWorkersAiConfiguration(
            baseAddress,
            options.AccountId,
            options.ApiToken,
            options.RequestTimeout,
            options.TextToImageModelId,
            options.TextToImageSteps);
    }
}
