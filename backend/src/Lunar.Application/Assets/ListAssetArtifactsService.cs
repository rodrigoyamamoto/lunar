using System.Diagnostics;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Workflows;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Assets;

public sealed class ListAssetArtifactsService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly IGenerationInputRecordRepository _generationInputRecordRepository;
    private readonly ILogger<ListAssetArtifactsService> _logger;

    public ListAssetArtifactsService(
        IAssetRepository assetRepository,
        IArtifactRepository artifactRepository,
        IGenerationInputRecordRepository generationInputRecordRepository,
        ILogger<ListAssetArtifactsService> logger)
    {
        ArgumentNullException.ThrowIfNull(assetRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(generationInputRecordRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _assetRepository = assetRepository;
        _artifactRepository = artifactRepository;
        _generationInputRecordRepository = generationInputRecordRepository;
        _logger = logger;
    }


    public async Task<Result<IReadOnlyList<AssetArtifactHistoryItem>>> ListAsync(
        AssetId assetId,
        CancellationToken cancellationToken = default)
    {
        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.AssetArtifactsListActivityName);

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.AssetIdTag, assetId.Value.ToString());
        }

        var asset = await _assetRepository.GetAsync(assetId, cancellationToken);

        if (asset is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<IReadOnlyList<AssetArtifactHistoryItem>>.Failure(
                new AssetNotFound(assetId));
        }

        var artifacts = await _artifactRepository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        var generationInputs = await _generationInputRecordRepository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        var inputsByExecutionId = generationInputs
            .ToDictionary(record => record.WorkflowExecutionId);

        var ordered = artifacts
            .OrderByDescending(artifact => artifact.CreatedAt)
            .ThenByDescending(artifact => artifact.Id.Value)
            .Select(artifact => new AssetArtifactHistoryItem(
                artifact,
                TryResolveGenerationInput(artifact, inputsByExecutionId)))
            .ToList()
            .AsReadOnly();

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.ArtifactCountTag, ordered.Count);
            activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        return Result<IReadOnlyList<AssetArtifactHistoryItem>>.Success(ordered);
    }


    private static GenerationInputRecord? TryResolveGenerationInput(
        Artifact artifact,
        IReadOnlyDictionary<WorkflowExecutionId, GenerationInputRecord> inputsByExecutionId)
    {
        if (artifact.SourceExecutionId is not { } executionId)
        {
            return null;
        }

        return inputsByExecutionId.TryGetValue(executionId, out var record)
            ? record
            : null;
    }
}
