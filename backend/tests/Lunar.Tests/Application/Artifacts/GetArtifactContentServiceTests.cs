using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Application.Artifacts;

public class GetArtifactContentServiceTests
{
    private static readonly ArtifactId SharedArtifactId = ArtifactId.New();
    private static readonly AssetId SharedAssetId = AssetId.New();

    private static readonly Artifact SharedArtifact = new(
        SharedArtifactId,
        SharedAssetId,
        "test-image.jpg",
        ArtifactType.ConceptImage,
        Array.Empty<ArtifactId>(),
        WorkflowExecutionId.New());

    private static readonly BinaryArtifactContent SharedContent =
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg");


    [Fact]
    public void Constructor_NullArtifactRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetArtifactContentService(
                null!,
                new TrackingArtifactContentStore()));
    }

    [Fact]
    public void Constructor_NullContentStore_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GetArtifactContentService(
                new TrackingArtifactRepository(),
                null!));
    }

    [Fact]
    public async Task GetAsync_EmptyArtifactId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GetAsync(new ArtifactId(Guid.Empty)));
    }

    [Fact]
    public async Task GetAsync_MissingMetadata_ShouldReturnArtifactNotFound()
    {
        var service = CreateService();

        var result = await service.GetAsync(SharedArtifactId);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactNotFound>(result.Error);
        Assert.Equal(SharedArtifactId, error.ArtifactId);
    }

    [Fact]
    public async Task GetAsync_MetadataExistsContentMissing_ShouldReturnArtifactContentNotFound()
    {
        var artifactRepository = new TrackingArtifactRepository(SharedArtifact);
        var contentStore = new TrackingArtifactContentStore(content: null);

        var service = CreateService(
            artifactRepository: artifactRepository,
            contentStore: contentStore);

        var result = await service.GetAsync(SharedArtifactId);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactContentNotFound>(result.Error);
        Assert.Equal(SharedArtifactId, error.ArtifactId);
    }

    [Fact]
    public async Task GetAsync_Success_ShouldReturnExactArtifactAndContent()
    {
        var artifactRepository = new TrackingArtifactRepository(SharedArtifact);
        var contentStore = new TrackingArtifactContentStore(content: SharedContent);

        var service = CreateService(
            artifactRepository: artifactRepository,
            contentStore: contentStore);

        var result = await service.GetAsync(SharedArtifactId);

        Assert.True(result.IsSuccess);
        Assert.Same(SharedArtifact, result.Value!.Artifact);
        Assert.Same(SharedContent, result.Value!.Content);
    }

    [Fact]
    public async Task GetAsync_PreCancelledTokenDuringMetadataRead_ShouldPropagate()
    {
        var artifactRepository = new TrackingArtifactRepository(SharedArtifact);
        var contentStore = new TrackingArtifactContentStore(content: SharedContent);

        var service = CreateService(
            artifactRepository: artifactRepository,
            contentStore: contentStore);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetAsync(SharedArtifactId, cts.Token));
    }

    [Fact]
    public async Task GetAsync_InvalidDataExceptionFromContentStore_ShouldPropagate()
    {
        var artifactRepository = new TrackingArtifactRepository(SharedArtifact);
        var contentStore = new ThrowingArtifactContentStore(
            new InvalidDataException("Corrupt metadata."));

        var service = CreateService(
            artifactRepository: artifactRepository,
            contentStore: contentStore);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GetAsync(SharedArtifactId));
    }

    [Fact]
    public async Task GetAsync_UnexpectedRepositoryException_ShouldPropagate()
    {
        var artifactRepository = new ThrowingArtifactRepository(
            new InvalidOperationException("Database connection lost."));
        var contentStore = new TrackingArtifactContentStore(content: SharedContent);

        var service = CreateService(
            artifactRepository: artifactRepository,
            contentStore: contentStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(SharedArtifactId));
    }


    private static GetArtifactContentService CreateService(
        IArtifactRepository? artifactRepository = null,
        IArtifactContentStore? contentStore = null)
    {
        return new GetArtifactContentService(
            artifactRepository ?? new TrackingArtifactRepository(),
            contentStore ?? new TrackingArtifactContentStore());
    }


    private sealed class TrackingArtifactRepository : IArtifactRepository
    {
        private readonly Artifact? _artifact;

        public TrackingArtifactRepository(Artifact? artifact = null)
        {
            _artifact = artifact;
        }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_artifact);
        }
    }


    private sealed class ThrowingArtifactRepository : IArtifactRepository
    {
        private readonly Exception _exception;

        public ThrowingArtifactRepository(Exception exception)
        {
            _exception = exception;
        }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }


    private sealed class TrackingArtifactContentStore : IArtifactContentStore
    {
        private readonly ArtifactContent? _content;

        public TrackingArtifactContentStore(ArtifactContent? content = null)
        {
            _content = content;
        }

        public Task<bool> TryAddAsync(
            ArtifactId artifactId,
            ArtifactContent content,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }

        public Task<ArtifactContent?> GetAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            if (artifactId.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(artifactId));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_content);
        }

        public Task<bool> TryDeleteAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(false);
        }
    }


    private sealed class ThrowingArtifactContentStore : IArtifactContentStore
    {
        private readonly Exception _exception;

        public ThrowingArtifactContentStore(Exception exception)
        {
            _exception = exception;
        }

        public Task<bool> TryAddAsync(
            ArtifactId artifactId,
            ArtifactContent content,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<ArtifactContent?> GetAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }

        public Task<bool> TryDeleteAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }
}
