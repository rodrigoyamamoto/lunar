using System.Diagnostics;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Workflows;

public sealed class GenerateDefaultArtifactService
{
    private readonly GenerateArtifactService _generateArtifactService;
    private readonly GenerationWorkflowTarget _target;
    private readonly ILogger<GenerateDefaultArtifactService> _logger;

    public GenerateDefaultArtifactService(
        GenerateArtifactService generateArtifactService,
        GenerationWorkflowTarget target,
        ILogger<GenerateDefaultArtifactService> logger)
    {
        ArgumentNullException.ThrowIfNull(generateArtifactService);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);

        _generateArtifactService = generateArtifactService;
        _target = target;
        _logger = logger;
    }


    public async Task<Result<GeneratedArtifact>> GenerateAsync(
        AssetId assetId,
        CapabilityExecutionInput input,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        ArgumentNullException.ThrowIfNull(input);

        var promptLength = GetPromptLength(input);
        var stopwatch = Stopwatch.StartNew();

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.GenerationActivityName);

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.AssetIdTag, assetId.Value.ToString());
            activity.SetTag(ApplicationTelemetry.WorkflowDefinitionIdTag, _target.WorkflowDefinitionId.Value.ToString());
            activity.SetTag(ApplicationTelemetry.WorkflowDefinitionVersionTag, _target.Version);
            activity.SetTag(ApplicationTelemetry.WorkflowStepPositionTag, _target.StepPosition);
        }

        _logger.LogInformation(
            "Generation started. AssetId={AssetId} PromptLength={PromptLength}",
            assetId.Value,
            promptLength);

        try
        {
            var result = await _generateArtifactService.GenerateAsync(
                assetId,
                _target.WorkflowDefinitionId,
                _target.Version,
                _target.StepPosition,
                input,
                cancellationToken);

            stopwatch.Stop();
            var durationMs = stopwatch.Elapsed.TotalMilliseconds;

            if (result.IsSuccess)
            {
                var generated = result.Value!;
                var artifact = generated.ProducedArtifact.Artifact;

                if (activity is not null)
                {
                    activity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, generated.WorkflowExecutionId.Value.ToString());
                    activity.SetTag(ApplicationTelemetry.ArtifactIdTag, artifact.Id.Value.ToString());
                    activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
                    activity.SetStatus(ActivityStatusCode.Ok);
                }

                ApplicationTelemetry.GenerationAttempts.Add(
                    1,
                    new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeSuccess));

                ApplicationTelemetry.GenerationDuration.Record(
                    durationMs,
                    new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeSuccess));

                _logger.LogInformation(
                    "Generation completed. AssetId={AssetId} WorkflowExecutionId={WorkflowExecutionId} ArtifactId={ArtifactId} DurationMs={DurationMs:F0}",
                    assetId.Value,
                    generated.WorkflowExecutionId.Value,
                    artifact.Id.Value,
                    durationMs);

                return result;
            }

            var (stage, kind) = FailureStageClassifier.Classify(result.Error!);
            var errorType = result.Error!.GetType().Name;

            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetTag(ApplicationTelemetry.FailureStageTag, stage);
                if (kind is not null)
                {
                    activity.SetTag(ApplicationTelemetry.FailureKindTag, kind);
                }
                activity.SetStatus(ActivityStatusCode.Error);
            }

            ApplicationTelemetry.GenerationAttempts.Add(
                1,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeFailure));

            ApplicationTelemetry.GenerationDuration.Record(
                durationMs,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeFailure));

            _logger.LogWarning(
                "Generation failed. AssetId={AssetId} Stage={Stage} ErrorType={ErrorType} DurationMs={DurationMs:F0}{FailureKind}",
                assetId.Value,
                stage,
                errorType,
                durationMs,
                kind is not null ? $" FailureKind={kind}" : string.Empty);

            return result;
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();

            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeCancelled);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            ApplicationTelemetry.GenerationAttempts.Add(
                1,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeCancelled));

            ApplicationTelemetry.GenerationDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeCancelled));

            _logger.LogWarning(
                "Generation cancelled. AssetId={AssetId} DurationMs={DurationMs:F0}",
                assetId.Value,
                stopwatch.Elapsed.TotalMilliseconds);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetTag(ApplicationTelemetry.FailureStageTag, ApplicationTelemetry.StageApplication);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            ApplicationTelemetry.GenerationAttempts.Add(
                1,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeFailure));

            ApplicationTelemetry.GenerationDuration.Record(
                stopwatch.Elapsed.TotalMilliseconds,
                new KeyValuePair<string, object?>(ApplicationTelemetry.OutcomeTag, ApplicationTelemetry.OutcomeFailure));

            _logger.LogError(ex,
                "Generation crashed. AssetId={AssetId} DurationMs={DurationMs:F0}",
                assetId.Value,
                stopwatch.Elapsed.TotalMilliseconds);

            throw;
        }
    }


    private static int GetPromptLength(CapabilityExecutionInput input)
    {
        return input is TextPromptInput textPrompt ? textPrompt.Prompt.Length : 0;
    }
}
