using System.Diagnostics;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Microsoft.Extensions.Logging;

namespace Lunar.Application.Artifacts;

public sealed class GetArtifactContentService
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IArtifactContentStore _artifactContentStore;
    private readonly ILogger<GetArtifactContentService> _logger;

    public GetArtifactContentService(
        IArtifactRepository artifactRepository,
        IArtifactContentStore artifactContentStore,
        ILogger<GetArtifactContentService> logger)
    {
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(artifactContentStore);
        ArgumentNullException.ThrowIfNull(logger);

        _artifactRepository = artifactRepository;
        _artifactContentStore = artifactContentStore;
        _logger = logger;
    }


    public async Task<Result<ProducedArtifact>> GetAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken = default)
    {
        if (artifactId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(artifactId));
        }

        using var activity = ApplicationTelemetry.ActivitySource.StartActivity(
            ApplicationTelemetry.ArtifactContentGetActivityName);

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.ArtifactIdTag, artifactId.Value.ToString());
        }

        var artifact = await _artifactRepository.GetAsync(
            artifactId,
            cancellationToken);

        if (artifact is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<ProducedArtifact>.Failure(
                new ArtifactNotFound(artifactId));
        }

        if (activity is not null)
        {
            activity.SetTag(ApplicationTelemetry.AssetIdTag, artifact.AssetId.Value.ToString());
        }

        var content = await _artifactContentStore.GetAsync(
            artifactId,
            cancellationToken);

        if (content is null)
        {
            if (activity is not null)
            {
                activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeFailure);
                activity.SetStatus(ActivityStatusCode.Error);
            }

            return Result<ProducedArtifact>.Failure(
                new ArtifactContentNotFound(artifactId));
        }

        if (activity is not null)
        {
            if (content is BinaryArtifactContent binary)
            {
                activity.SetTag(ApplicationTelemetry.ContentMediaTypeTag, binary.MediaType);
                activity.SetTag(ApplicationTelemetry.ContentSizeBytesTag, binary.Data.Length);
            }

            activity.SetTag(ApplicationTelemetry.OperationOutcomeTag, ApplicationTelemetry.OutcomeSuccess);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        return Result<ProducedArtifact>.Success(
            new ProducedArtifact(artifact, content));
    }
}
