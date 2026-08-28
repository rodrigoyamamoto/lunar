using System.Diagnostics;
using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareWorkersAiTextToImageExecutor : ICapabilityExecutor
{
    private const string OutputMediaType = "image/jpeg";
    private const string ProviderName = InfrastructureTelemetry.ProviderCloudflareWorkersAi;

    private readonly CloudflareWorkersAiClient _client;
    private readonly string _modelId;
    private readonly ILogger<CloudflareWorkersAiTextToImageExecutor> _logger;

    public CloudflareWorkersAiTextToImageExecutor(
        CloudflareWorkersAiClient client,
        ILogger<CloudflareWorkersAiTextToImageExecutor> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(logger);

        _client = client;
        _logger = logger;
        _modelId = client.TextToImageModelId;
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

        var model = _modelId;
        var stopwatch = Stopwatch.StartNew();

        using var activity = InfrastructureTelemetry.ActivitySource.StartActivity(
            InfrastructureTelemetry.ProviderRequestActivityName);

        if (activity is not null)
        {
            activity.SetTag(InfrastructureTelemetry.ProviderNameTag, ProviderName);
            activity.SetTag(InfrastructureTelemetry.ProviderModelTag, model);
        }

        InfrastructureTelemetry.ProviderRequests.Add(
            1,
            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model));

        try
        {
            var result = await _client.GenerateImageAsync(
                textPromptInput.Prompt,
                cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            switch (result)
            {
                case CloudflareImageGenerationSucceeded succeeded:
                    {
                        var outputSizeBytes = succeeded.ImageBytes.Length;

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
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeSuccess));

                        InfrastructureTelemetry.ProviderOutputSize.Record(
                            outputSizeBytes,
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderNameMetricTag, ProviderName),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model));

                        _logger.LogInformation(
                            "Provider generation completed. Provider={Provider} Model={Model} HttpStatus=200 DurationMs={DurationMs:F0} OutputSizeBytes={OutputSizeBytes}",
                            ProviderName,
                            model,
                            durationMs,
                            outputSizeBytes);

                        return new CapabilityExecutionSucceeded(
                            new CapabilityExecutionOutput(
                                new BinaryArtifactContent(succeeded.ImageBytes, OutputMediaType)));
                    }

                case CloudflareImageGenerationFailed failed:
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
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeFailure),
                            new KeyValuePair<string, object?>(InfrastructureTelemetry.FailureKindMetricTag, failureKind));

                        var retryAfterSeconds = failed.Failure.RetryAfter is { } retryAfter
                            ? (int)Math.Ceiling(retryAfter.TotalSeconds)
                            : (int?)null;

                        _logger.LogWarning(
                            "Provider generation failed. Provider={Provider} Model={Model} FailureKind={FailureKind} DurationMs={DurationMs:F0}{RetryAfter}",
                            ProviderName,
                            model,
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
                        "Cloudflare client returned an unsupported result.");
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
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model),
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
                new KeyValuePair<string, object?>(InfrastructureTelemetry.ProviderModelMetricTag, model),
                new KeyValuePair<string, object?>(InfrastructureTelemetry.OutcomeTag, InfrastructureTelemetry.OutcomeFailure));

            throw;
        }
    }
}
