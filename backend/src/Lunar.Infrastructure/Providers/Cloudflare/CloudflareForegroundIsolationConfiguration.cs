namespace Lunar.Infrastructure.Providers.Cloudflare;

/// <summary>
/// Immutable, validated configuration for the Cloudflare Images
/// foreground-isolation provider adapter.
///
/// Foreground isolation is an optional provider-backed capability.
/// The capability is <c>disabled</c> when both <c>Endpoint</c> and
/// <c>ServiceToken</c> are blank/whitespace. When disabled, the
/// application starts normally and the foreground-isolation
/// <c>CapabilityId</c> is left unresolved by
/// <c>ICapabilityExecutorResolver</c>.
///
/// When the capability is not disabled (at least one of
/// <c>Endpoint</c> or <c>ServiceToken</c> is non-blank), it must be
/// fully and validly configured or startup fails with
/// <c>OptionsValidationException</c>.
///
/// This class is the single authority for the
/// disabled/enabled/valid classification used by both startup
/// validation and the composition root.
/// </summary>
public sealed class CloudflareForegroundIsolationConfiguration
{
    private const string ValidationMessage =
        "CloudflareForegroundIsolation must either be disabled (Endpoint and ServiceToken both blank) "
        + "or fully configured with an absolute HTTPS Endpoint, nonblank ServiceToken, "
        + "and strictly positive RequestTimeout.";

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


    /// <summary>
    /// Returns <c>true</c> when foreground isolation is disabled,
    /// i.e. both <c>Endpoint</c> and <c>ServiceToken</c> are
    /// blank/whitespace. <c>RequestTimeout</c> alone never enables
    /// the capability.
    /// </summary>
    public static bool IsDisabled(CloudflareForegroundIsolationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return string.IsNullOrWhiteSpace(options.Endpoint)
               && string.IsNullOrWhiteSpace(options.ServiceToken);
    }

    /// <summary>
    /// Returns <c>true</c> when the configuration is fully and validly
    /// supplied for an enabled capability. This is the case only when
    /// the capability is not disabled and all enabled-validation
    /// rules pass.
    /// </summary>
    public static bool IsValid(CloudflareForegroundIsolationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsDisabled(options))
        {
            return false;
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return false;
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.ServiceToken))
        {
            return false;
        }

        return options.RequestTimeout > TimeSpan.Zero;
    }

    /// <summary>
    /// Returns <c>true</c> when the options represent a valid startup
    /// state: either disabled or fully and validly configured.
    /// </summary>
    public static bool IsAcceptable(CloudflareForegroundIsolationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return IsDisabled(options) || IsValid(options);
    }

    /// <summary>
    /// Builds a <see cref="CloudflareForegroundIsolationConfiguration"/>
    /// from fully valid enabled options. Throws if the options are
    /// disabled or invalid. Use <see cref="IsDisabled"/> or
    /// <see cref="IsValid"/> before calling this from optional paths.
    /// </summary>
    public static CloudflareForegroundIsolationConfiguration From(CloudflareForegroundIsolationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (IsDisabled(options))
        {
            throw new InvalidOperationException(
                "CloudflareForegroundIsolation is disabled; no configuration is available.");
        }

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpoint))
        {
            throw new InvalidOperationException(ValidationMessage);
        }

        if (endpoint.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(ValidationMessage);
        }

        if (string.IsNullOrWhiteSpace(options.ServiceToken))
        {
            throw new InvalidOperationException(ValidationMessage);
        }

        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException(ValidationMessage);
        }

        return new CloudflareForegroundIsolationConfiguration(
            endpoint,
            options.ServiceToken,
            options.RequestTimeout);
    }

    /// <summary>
    /// The bounded, secret-free validation message used by startup
    /// validation and <see cref="From"/>. Exposed so the composition
    /// root and tests reference a single authoritative message.
    /// </summary>
    public static string GetValidationMessage() => ValidationMessage;
}
