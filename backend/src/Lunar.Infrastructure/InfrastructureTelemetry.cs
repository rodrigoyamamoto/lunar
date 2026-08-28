using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Lunar.Infrastructure;

/// <summary>
/// Owns the long-lived reusable <see cref="ActivitySource"/> and
/// <see cref="Meter"/> for the Lunar Infrastructure layer. These are
/// OpenTelemetry-compatible BCL instrumentation objects. No OpenTelemetry
/// SDK or exporter is configured yet.
/// </summary>
public static class InfrastructureTelemetry
{
    public const string ActivitySourceName = "Lunar.Infrastructure";

    public const string MeterName = "Lunar.Infrastructure";

    public static ActivitySource ActivitySource { get; } = new(ActivitySourceName, "1.0.0");

    public static Meter Meter { get; } = new(MeterName, "1.0.0");

    // --- Activity names ---

    public const string ProviderRequestActivityName = "lunar.provider.request";
    public const string ContentStoreWriteActivityName = "lunar.content_store.write";
    public const string ContentStoreReadActivityName = "lunar.content_store.read";

    // --- Trace tag names (Infrastructure-specific) ---

    public const string ProviderNameTag = "lunar.provider.name";
    public const string ProviderModelTag = "lunar.provider.model";
    public const string ProviderOperationTag = "lunar.provider.operation";
    public const string ProviderHttpStatusTag = "lunar.provider.http_status";
    public const string ArtifactIdTag = "lunar.artifact.id";
    public const string ContentMediaTypeTag = "lunar.content.media_type";
    public const string ContentSizeBytesTag = "lunar.content.size_bytes";
    public const string OperationOutcomeTag = "lunar.operation.outcome";
    public const string FailureKindTag = "lunar.failure.kind";
    public const string ExceptionTypeTag = "exception.type";

    // --- Metric instruments ---

    public static Counter<long> ProviderRequests { get; } =
        Meter.CreateCounter<long>("lunar.provider.requests", unit: "{request}");

    public static Histogram<double> ProviderRequestDuration { get; } =
        Meter.CreateHistogram<double>("lunar.provider.request.duration", unit: "ms");

    public static Histogram<long> ProviderOutputSize { get; } =
        Meter.CreateHistogram<long>("lunar.provider.output.size", unit: "By");

    // --- Metric tag names (low-cardinality only) ---

    public const string ProviderNameMetricTag = "provider";
    public const string ProviderModelMetricTag = "model";
    public const string ProviderOperationMetricTag = "operation";
    public const string OutcomeTag = "outcome";
    public const string FailureKindMetricTag = "failure_kind";

    // --- Bounded provider/model/operation values ---

    public const string ProviderCloudflareWorkersAi = "cloudflare_workers_ai";
    public const string ProviderCloudflareImages = "cloudflare_images";

    public const string OperationForegroundIsolation = "foreground_isolation";

    // --- Outcome values (shared semantics with ApplicationTelemetry) ---

    public const string OutcomeSuccess = "success";
    public const string OutcomeFailure = "failure";
    public const string OutcomeCancelled = "cancelled";
}
