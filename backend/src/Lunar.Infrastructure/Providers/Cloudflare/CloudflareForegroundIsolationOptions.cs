namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareForegroundIsolationOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string ServiceToken { get; set; } = string.Empty;

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);
}
