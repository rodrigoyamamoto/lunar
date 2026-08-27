using System.Net;
using System.Net.Http.Json;
using Lunar.Api.Contracts;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Api;

public class ArtifactContentApiTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public ArtifactContentApiTests(LunarApiFactory factory)
    {
        _factory = factory;
        _factory.Executor.Reset();
    }


    [Fact]
    public async Task GetArtifactContent_AfterSuccessfulGeneration_ReturnsExactBytesAndMediaType()
    {
        var assetId = await _factory.SeedAssetAsync();

        var generationResponse = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "test prompt"
        });

        var executorCallsBeforeGet = _factory.Executor.CallCount;

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/artifacts/{generationResponse.ArtifactId}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 },
            bytes);

        Assert.Equal(executorCallsBeforeGet, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task GetArtifactContent_WithMissingArtifact_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/artifacts/{Guid.NewGuid()}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("artifact_not_found", body!.Code);
    }


    [Fact]
    public async Task GetArtifactContent_WithMetadataButMissingContent_Returns404()
    {
        using var tempRoot = new TempContentRoot("lunar-empty-content");
        var artifactRepository = new InMemoryArtifactRepository();
        var contentStore = new LocalFileArtifactContentStore(tempRoot.Path, NullLogger<LocalFileArtifactContentStore>.Instance);

        var artifactId = ArtifactId.New();
        var assetId = AssetId.New();
        var artifact = new Artifact(
            artifactId,
            assetId,
            "orphan.jpg",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            WorkflowExecutionId.New());

        await artifactRepository.TryAddAsync(artifact);

        var client = _factory.CreateClientWithServices(services =>
        {
            services.RemoveAll<IArtifactRepository>();
            services.AddSingleton<IArtifactRepository>(artifactRepository);

            services.RemoveAll<IArtifactContentStore>();
            services.AddSingleton<IArtifactContentStore>(contentStore);
        });

        var response = await client.GetAsync($"/api/artifacts/{artifactId.Value}/content");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("artifact_content_not_found", body!.Code);
    }


    [Fact]
    public async Task GetArtifactContent_WithCorruptDurableState_PropagatesInvalidDataException()
    {
        using var tempRoot = new TempContentRoot("lunar-corrupt");
        var contentStore = new LocalFileArtifactContentStore(tempRoot.Path, NullLogger<LocalFileArtifactContentStore>.Instance);
        var artifactRepository = new InMemoryArtifactRepository();

        var artifactId = ArtifactId.New();
        var assetId = AssetId.New();
        var content = new BinaryArtifactContent(
            new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 },
            "image/jpeg");

        var artifact = new Artifact(
            artifactId,
            assetId,
            "test.jpg",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>(),
            WorkflowExecutionId.New());

        await artifactRepository.TryAddAsync(artifact);
        await contentStore.TryAddAsync(artifactId, content);

        var artifactDir = System.IO.Path.Combine(tempRoot.Path, artifactId.Value.ToString("N"));
        var metadataPath = System.IO.Path.Combine(artifactDir, "metadata.json");
        File.WriteAllText(metadataPath, "{ this is not valid json }");

        var client = _factory.CreateClientWithServices(services =>
        {
            services.RemoveAll<IArtifactRepository>();
            services.AddSingleton<IArtifactRepository>(artifactRepository);

            services.RemoveAll<IArtifactContentStore>();
            services.AddSingleton<IArtifactContentStore>(contentStore);
        });

        // The TestServer propagates unhandled exceptions from the request pipeline.
        // Corrupt durable state must surface as InvalidDataException, not be
        // silently converted to a false 404 "artifact_content_not_found".
        var exception = await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
            client.GetAsync($"/api/artifacts/{artifactId.Value}/content"));

        Assert.Contains("malformed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task GetArtifactContent_ResponseDoesNotContainStorageDetails()
    {
        var assetId = await _factory.SeedAssetAsync();

        var generationResponse = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "test prompt"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/artifacts/{generationResponse.ArtifactId}/content");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);

        var bodyBytes = await response.Content.ReadAsByteArrayAsync();
        var bodyString = System.Text.Encoding.UTF8.GetString(bodyBytes);

        Assert.DoesNotContain("content.bin", bodyString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata.json", bodyString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".tmp-", bodyString, StringComparison.OrdinalIgnoreCase);
    }
}
