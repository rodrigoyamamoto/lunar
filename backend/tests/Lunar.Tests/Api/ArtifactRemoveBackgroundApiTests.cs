using System.Net;
using System.Net.Http.Json;
using Lunar.Api.Contracts;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Api;

public class ArtifactRemoveBackgroundApiTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public ArtifactRemoveBackgroundApiTests(LunarApiFactory factory)
    {
        _factory = factory;
    }


    [Fact]
    public async Task RemoveBackground_MalformedArtifactId_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync("/api/artifacts/not-a-guid/remove-background", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RemoveBackground_EmptyArtifactId_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/artifacts/00000000-0000-0000-0000-000000000000/remove-background",
            null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_artifact_id", body!.Code);
    }

    [Fact]
    public async Task RemoveBackground_MissingArtifact_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/artifacts/{ArtifactId.New().Value}/remove-background",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("artifact_not_found", body!.Code);
    }

    [Fact]
    public async Task RemoveBackground_Success_Returns201WithLineage()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A sword"
        });

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background",
            null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ArtifactTransformationResponse>();
        Assert.NotNull(body);
        Assert.Equal(assetId.Value, body!.AssetId);
        Assert.NotEqual(generation.ArtifactId, body.ArtifactId);
        Assert.Equal("image/png", body.MediaType);
        Assert.Single(body.SourceArtifactIds);
        Assert.Equal(generation.ArtifactId, body.SourceArtifactIds[0]);
        Assert.Equal($"/api/artifacts/{body.ArtifactId}/content", body.ContentUrl);
    }

    [Fact]
    public async Task RemoveBackground_DerivedArtifactPreservesSourceTypeAndApplicationNaming()
    {
        // Seed a source Artifact with a non-ConceptImage type to prove
        // the derived Artifact preserves the source type rather than
        // hard-coding ConceptImage.
        var assetId = await _factory.SeedAssetAsync("Sprite Sheet Asset");
        var sourceArtifactId = await _factory.SeedArtifactAsync(
            assetId,
            name: "Knight Sprite",
            type: ArtifactType.Texture,
            mediaType: "image/png",
            content: new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            $"/api/artifacts/{sourceArtifactId.Value}/remove-background",
            null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ArtifactTransformationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Texture", body!.ArtifactType);
        Assert.Equal("Knight Sprite - background removed", body.ArtifactName);
        Assert.Single(body.SourceArtifactIds);
        Assert.Equal(sourceArtifactId.Value, body.SourceArtifactIds[0]);
    }

    [Fact]
    public async Task RemoveBackground_DerivedArtifactContentEndpointReturnsPng()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A sword"
        });

        var client = _factory.CreateClient();
        var transformResponse = await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background",
            null);
        var transformBody = await transformResponse.Content.ReadFromJsonAsync<ArtifactTransformationResponse>();

        var contentResponse = await client.GetAsync(transformBody!.ContentUrl);
        contentResponse.EnsureSuccessStatusCode();
        Assert.Equal("image/png", contentResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RemoveBackground_SourceArtifactRemainsUnchanged()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A sword"
        });

        var client = _factory.CreateClient();
        await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background",
            null);

        var listResponse = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");
        listResponse.EnsureSuccessStatusCode();

        var list = await listResponse.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);

        var source = list.Single(a => a.ArtifactId == generation.ArtifactId);
        Assert.Empty(source.SourceArtifactIds);
        Assert.Equal("Generated image", source.ArtifactName);
        Assert.Equal("ConceptImage", source.ArtifactType);
    }

    [Fact]
    public async Task RemoveBackground_Success_GalleryContainsDerivedArtifactWithLineage()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A sword"
        });

        var client = _factory.CreateClient();
        await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background",
            null);

        var listResponse = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");
        listResponse.EnsureSuccessStatusCode();

        var list = await listResponse.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);

        var derived = list[0];
        var source = list[1];

        Assert.Single(derived.SourceArtifactIds);
        Assert.Equal(source.ArtifactId, derived.SourceArtifactIds[0]);
        Assert.Empty(source.SourceArtifactIds);
    }

    [Fact]
    public async Task RemoveBackground_DerivedArtifactCanAlsoRemoveBackground()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A sword"
        });

        var client = _factory.CreateClient();

        var firstResponse = await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background",
            null);
        var firstBody = await firstResponse.Content.ReadFromJsonAsync<ArtifactTransformationResponse>();

        var secondResponse = await client.PostAsync(
            $"/api/artifacts/{firstBody!.ArtifactId}/remove-background",
            null);

        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);

        var secondBody = await secondResponse.Content.ReadFromJsonAsync<ArtifactTransformationResponse>();
        Assert.NotNull(secondBody);
        Assert.Single(secondBody!.SourceArtifactIds);
        Assert.Equal(firstBody.ArtifactId, secondBody.SourceArtifactIds[0]);
    }

    [Fact]
    public async Task RemoveBackground_ApiJsonNeverContainsServiceTokenOrAuthorization()
    {
        var assetId = await _factory.SeedAssetAsync("Privacy Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A shield"
        });

        var client = _factory.CreateClient();

        // Transformation response
        var transformResponse = await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background", null);
        var transformBody = await transformResponse.Content.ReadAsStringAsync();

        // Gallery list response
        var listResponse = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");
        var listBody = await listResponse.Content.ReadAsStringAsync();

        // Error responses
        var errorResponse = await client.PostAsync(
            "/api/artifacts/00000000-0000-0000-0000-000000000000/remove-background", null);
        var errorBody = await errorResponse.Content.ReadAsStringAsync();

        var allBodies = $"{transformBody}\n{listBody}\n{errorBody}";

        Assert.DoesNotContain("test-token", allBodies);
        Assert.DoesNotContain("LUNAR_FOREGROUND_ISOLATION_TOKEN", allBodies);
        Assert.DoesNotContain("Authorization", allBodies);
        Assert.DoesNotContain("Bearer", allBodies);
    }

    [Fact]
    public async Task RemoveBackground_ApiJsonNeverContainsLocalPathOrProviderException()
    {
        var assetId = await _factory.SeedAssetAsync("Privacy Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "A shield"
        });

        var client = _factory.CreateClient();

        var transformResponse = await client.PostAsync(
            $"/api/artifacts/{generation.ArtifactId}/remove-background", null);
        var transformBody = await transformResponse.Content.ReadAsStringAsync();

        var listResponse = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");
        var listBody = await listResponse.Content.ReadAsStringAsync();

        var allBodies = $"{transformBody}\n{listBody}";

        Assert.DoesNotContain("data/artifacts", allBodies);
        Assert.DoesNotContain("LocalRootPath", allBodies);
        Assert.DoesNotContain("BindingInternalError", allBodies);
        Assert.DoesNotContain("IMAGES_SECRET_DETAIL", allBodies);
    }
}
