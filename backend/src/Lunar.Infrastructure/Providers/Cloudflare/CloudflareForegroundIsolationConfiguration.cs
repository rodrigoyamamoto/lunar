namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareForegroundIsolationConfiguration
{
    public Uri Endpoint { get; }

    public string ServiceToken { get; }

    public TimeSpan RequestTimeout { get; }


    private CloudflareForegroundIsolationConfiguration(
        Uri endpoint,
        string serviceToken,
        TimeSpan requestTimeout)
    {
        Endpoint = endpoint;
        ServiceToken = serviceToken;
        RequestTimeout = requestTimeout;
    }


    public static CloudflareForegroundIsolationConfiguration From(CloudflareForegroundIsolationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation:Endpoint is not configured.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation:Endpoint must be an absolute URI.");
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation:Endpoint must use HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceToken))
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation:ServiceToken is not configured.");
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation:RequestTimeout must be strictly positive.");
        }

        return new CloudflareForegroundIsolationConfiguration(
            endpoint,
            options.ServiceToken,
            options.RequestTimeout);
    }
}
