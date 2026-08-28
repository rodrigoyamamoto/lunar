using System.Diagnostics;
using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace Lunar.Infrastructure.Providers.Cloudflare;

/// <summary>
/// Infrastructure executor for foreground isolation using Cloudflare
/// Images via a Lunar-owned Worker adapter. Accepts
/// <see cref="ImageArtifactInput"/>, sends raw image bytes to the
/// Worker, and returns a transparent PNG
/// <see cref="CapabilityExecutionOutput"/>.
///
/// The executor only transforms bytes. Direct Artifact lineage is
/// owned by the Application/workflow execution context, not by this
/// executor.
/// </summary>
public sealed class CloudflareImagesForegroundIsolationExecutor : ICapabilityExecutor
{
    private const string OutputMediaType = "image/png";
    private const string ProviderName = InfrastructureTelemetry.ProviderCloudflareImages;
    private const string OperationName = InfrastructureTelemetry.OperationForegroundIsolation;

    private readonly CloudflareForegroundIsolationClient _client;
    private readonly ILogger<CloudflareImagesForegroundIsolationExecutor> _logger;

    public CloudflareImagesForegroundIsolationExecutor(
        CloudflareForegroundIsolationClient client,
        ILogger<CloudflareImagesForegroundIsolationExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
    }


    public async Task<CapabilityExecutionOutcome> ExecuteAsync(
        CapabilityExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Input is not ImageArtifactInput imageInput)
        {
            throw new ArgumentException(
                "CloudflareImagesForegroundIsolationExecutor requires ImageArtifactInput.",
                nameof(request));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var stopwatch = Stopwatch.StartNew();

        using var activity = InfrastructureTelemetry.ActivitySource.StartActivity(
            InfrastructureTelemetry.ProviderRequestActivityName);

        if (activity is not null)
        {
            activity.SetTag(InfrastructureTelemetry.ProviderNameTag, ProviderName);
            activity.SetTag(InfrastructureTelemetry.ProviderOperationTag, OperationName);
        }

        InfrastructureTelemetry.ProviderRequests.Add(
            1,
            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName));

        try
        {
            var result = await _client.IsolateForegroundAsync(
                imageInput.Content.Data.ToArray(),
                imageInput.Content.MediaType,
                cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            switch (result)
            {
                case CloudflareForegroundIsolationSucceeded succeeded:
                    {
                        var outputSizeBytes = succeeded.PngBytes.Length;

                        if (activity is not null)
                        {
                            activity.SetTag(InfrastructureTelemetry.ProviderHttpStatusTag, 200);
                            activity.SetTag(InfrastructureTelemetry.ContentSizeBytesTag, outputSizeBytes);
                            activity.SetTag(InfrastructureTelemetry.OperationOutcomeTag, InfrastructureTelemetry.OutcomeSuccess);
                            activity.SetStatus(ActivityStatusCode.Ok);
                        }

                        InfrastructureTelemetry.ProviderRequestDuration.Record(
                            durationMs,
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeSuccess));

                        InfrastructureTelemetry.ProviderOutputSize.Record(
                            outputSizeBytes,
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName));

                        _logger.LogInformation(
                            "Provider foreground isolation completed. Provider={Provider} Operation={Operation} HttpStatus=200 DurationMs={DurationMs:F0} OutputSizeBytes={OutputSizeBytes}",
                            ProviderName,
                            OperationName,
                            durationMs,
                            outputSizeBytes);

                        return new CapabilityExecutionSucceeded(
                            new CapabilityExecutionOutput(
                                new BinaryArtifactContent(succeeded.PngBytes, OutputMediaType)));
                    }

                case CloudflareForegroundIsolationFailed failed:
                    {
                        var failureKind = failed.Failure.Kind.ToString();

                        if (activity is not null)
                        {
                            activity.SetTag(InfrastructureTelemetry.FailureKindTag, failureKind);
                            activity.SetTag(InfrastructureTelemetry.OperationOutcomeTag, InfrastructureTelemetry.OutcomeFailure);
                            activity.SetStatus(ActivityStatusCode.Error);
                        }

                        InfrastructureTelemetry.ProviderRequestDuration.Record(
                            durationMs,
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeFailure),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.FailureKindMetricTag, failureKind));

                        var retryAfterSeconds = failed.Failure.RetryAfter is { } retryAfter
                            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                            : (int?)null;

                        _logger.LogWarning(
                            "Provider foreground isolation failed. Provider={Provider} Operation={Operation} FailureKind={FailureKind} DurationMs={DurationMs:F0}{RetryAfter}",
                            ProviderName,
                            OperationName,
                            failureKind,
                            durationMs,
                            retryAfterSeconds is { } seconds ? $" RetryAfterSeconds={seconds}" : string.Empty);

                        return new CapabilityExecutionFailed(
                            new CapabilityExecutionFailure(
                                failed.Failure.Kind,
                                failed.Failure.RetryAfter));
                    }

                default:
                    throw new InvalidOperationException(
                        "Cloudflare foreground isolation client returned an unsupported result.");
            }
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            if (activity is not null)
            {
                activity.SetTag(InfrastructureTelemetry.OperationOutcomeTag, InfrastructureTelemetry.OutcomeCancelled);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            InfrastructureTelemetry.ProviderRequestDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName),
                new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeCancelled));

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (activity is not null)
            {
                activity.SetTag(InfrastructureTelemetry.OperationOutcomeTag, InfrastructureTelemetry.OutcomeFailure);
                activity.SetTag(InfrastructureTelemetry.ExceptionTypeTag, ex.GetType().FullName);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            InfrastructureTelemetry.ProviderRequestDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderOperationMetricTag, OperationName),
                new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeFailure));

            throw;
        }
    }
}
