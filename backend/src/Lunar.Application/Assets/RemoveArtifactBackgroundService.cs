using System.Diagnostics;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Assets;

/// <summary>
/// Product-facing Application service that removes the background from
/// an existing image Artifact, producing a new transparent PNG Artifact
/// in the same Asset with direct lineage to the source.
///
/// The service loads the source Artifact and its content, validates
/// that the content is a supported image, and executes the built-in
/// foreground-isolation workflow through the existing workflow-execution
/// machinery. Direct lineage (<see cref="Artifact.SourceArtifactIds"/>),
/// Artifact name, and Artifact type are supplied by the Application through
/// <see cref="WorkflowStepArtifactContext"/>; <see cref="ImageArtifactInput"/>
/// carries only the resolved image content. The provider executor only
/// transforms bytes.
/// </summary>
public sealed class RemoveArtifactBackgroundService
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IArtifactContentStore _artifactContentStore;
    private readonly GenerateArtifactService _generateArtifactService;
    private readonly ForegroundIsolationWorkflowTarget _target;
    private readonly ILogger<RemoveArtifactBackgroundService> _logger;

    private static readonly HashSet<string> SupportedInputMediaTypes = new(
        new[] { "image/jpeg", "image/png", "image/webp", "image/gif" },
        StringComparer.OrdinalIgnoreCase);

    public RemoveArtifactBackgroundService(
        IArtifactRepository artifactRepository,
        IArtifactContentStore artifactContentStore,
        GenerateArtifactService generateArtifactService,
        ForegroundIsolationWorkflowTarget target,
        ILogger<RemoveArtifactBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(artifactContentStore);
        ArgumentNullException.ThrowIfNull(generateArtifactService);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);

        _artifactRepository = artifactRepository;
        _artifactContentStore = artifactContentStore;
        _generateArtifactService = generateArtifactService;
        _target = target;
        _logger = logger;
    }


    public async Task<Result<GeneratedArtifact>> RemoveBackgroundAsync(
        ArtifactId sourceArtifactId,
        CancellationToken cancellationToken = default)
    {
        if (sourceArtifactId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Source artifact identifier cannot be empty.",
                nameof(sourceArtifactId));
        }

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(
            "lunar.artifact.remove_background");

        var startedAt = Stopwatch.GetTimestamp();

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.ArtifactIdTag, sourceArtifactId.Value.ToString());
        }

        var sourceArtifact = await _artifactRepository.GetAsync(
            sourceArtifactId,
            cancellationToken);

        if (sourceArtifact is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new ArtifactNotFound(sourceArtifactId));
        }

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.AssetIdTag, sourceArtifact.AssetId.Value.ToString());
        }

        var content = await _artifactContentStore.GetAsync(
            sourceArtifactId,
            cancellationToken);

        if (content is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new ArtifactContentNotFound(sourceArtifactId));
        }

        if (content is not BinaryArtifactContent binaryContent)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new UnsupportedArtifactContent(sourceArtifactId, "non-binary"));
        }

        if (!SupportedInputMediaTypes.Contains(binaryContent.MediaType))
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<GeneratedArtifact>.Failure(
                new UnsupportedArtifactContent(sourceArtifactId, binaryContent.MediaType));
        }

        var input = new ImageArtifactInput(binaryContent);

        _logger.LogInformation(
            "Background removal started. SourceArtifactId={SourceArtifactId} AssetId={AssetId} MediaType={MediaType} SizeBytes={SizeBytes}",
            sourceArtifactId.Value,
            sourceArtifact.AssetId.Value,
            binaryContent.MediaType,
            binaryContent.Data.Length);

        var result = await _generateArtifactService.GenerateAsync(
            sourceArtifact.AssetId,
            _target.WorkflowDefinitionId,
            _target.Version,
            _target.StepPosition,
            input,
            new WorkflowStepArtifactContext(
                $"{sourceArtifact.Name} - background removed",
                sourceArtifact.Type,
                new[] { sourceArtifact.Id }),
            cancellationToken);

        if (result.IsSuccess)
        {
            var durationMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.WorkflowExecutionIdTag, result.Value!.WorkflowExecutionId.Value.ToString());
                activity.SetTag(ApplicationTelemetry.DerivedArtifactIdTag, result.Value!.ProducedArtifact.Artifact.Id.Value.ToString());
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
                activity.SetStatus(ActivityStatusCode.Ok);
            }

            _logger.LogInformation(
                "Background removal completed. SourceArtifactId={SourceArtifactId} DerivedArtifactId={DerivedArtifactId} DurationMs={DurationMs:F0}",
                sourceArtifactId.Value,
                result.Value!.ProducedArtifact.Artifact.Id.Value,
                durationMs);
        }
        else
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            _logger.LogWarning(
                "Background removal failed. SourceArtifactId={SourceArtifactId} ErrorType={ErrorType}",
                sourceArtifactId.Value,
                result.Error!.GetType().Name);
        }

        return result;
    }
}
