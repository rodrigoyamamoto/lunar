using Lunar.Application.Errors;
using Lunar.Core.Artifacts;

namespace Lunar.Application.Artifacts;

public sealed class GetArtifactContentService
{
    private readonly IArtifactRepository _artifactRepository;
    private readonly IArtifactContentStore _artifactContentStore;

    public GetArtifactContentService(
        IArtifactRepository artifactRepository,
        IArtifactContentStore artifactContentStore)
    {
        ArgumentNullException.ThrowIfNull(artifactRepository);
        ArgumentNullException.ThrowIfNull(artifactContentStore);

        _artifactRepository = artifactRepository;
        _artifactContentStore = artifactContentStore;
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

        var artifact = await _artifactRepository.GetAsync(
            artifactId,
            cancellationToken);

        if (artifact is null)
        {
            return Result<ProducedArtifact>.Failure(
                new ArtifactNotFound(artifactId));
        }

        var content = await _artifactContentStore.GetAsync(
            artifactId,
            cancellationToken);

        if (content is null)
        {
            return Result<ProducedArtifact>.Failure(
                new ArtifactContentNotFound(artifactId));
        }

        return Result<ProducedArtifact>.Success(
            new ProducedArtifact(artifact, content));
    }
}
