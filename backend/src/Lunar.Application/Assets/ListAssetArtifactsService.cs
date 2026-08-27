using System.Diagnostics;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Assets;

public sealed class ListAssetArtifactsService
{
    private readonly IAssetRepository _assetRepository;
    private readonly IArtifactRepository _artifactRepository;
    private readonly ILogger<ListAssetArtifactsService> _logger;

    public ListAssetArtifactsService(
        IAssetRepository assetRepository,
        IArtifactRepository artifactRepository,
        ILogger<ListAssetArtifactsService> logger)
    {
        ArgumentNullException.ThrowIfNull(assetRepository);
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(logger);

        _assetRepository = assetRepository;
        _artifactRepository = artifactRepository;
        _logger = logger;
    }


    public async Task<Result<IReadOnlyList<Artifact>>> ListAsync(
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

            return Result<IReadOnlyList<Artifact>>.Failure(
                new AssetNotFound(assetId));
        }

        var artifacts = await _artifactRepository.GetByAssetIdAsync(
            assetId,
            cancellationToken);

        var ordered = artifacts
            .OrderByDescending(artifact => artifact.CreatedAt)
            .ThenByDescending(artifact => artifact.Id.Value)
            .ToList()
            .AsReadOnly();

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.ArtifactCountTag, ordered.Count);
            activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        return Result<IReadOnlyList<Artifact>>.Success(ordered);
    }
}
