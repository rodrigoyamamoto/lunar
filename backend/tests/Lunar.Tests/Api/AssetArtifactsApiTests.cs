using System.Net;
using System.Net.Http.Json;
using Lunar.Api.Contracts;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;

namespace Lunar.Tests.Api;

public class AssetArtifactsApiTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public AssetArtifactsApiTests(LunarApiFactory factory)
    {
        _factory = factory;
    }


    [Fact]
    public async Task ListArtifacts_MalformedAssetId_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/assets/not-a-guid/artifacts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task ListArtifacts_EmptyAssetId_Returns400()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/assets/00000000-0000-0000-0000-000000000000/artifacts");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_asset_id", body!.Code);
    }


    [Fact]
    public async Task ListArtifacts_MissingAsset_Returns404AssetNotFound()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{AssetId.New().Value}/artifacts");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("asset_not_found", body!.Code);
    }


    [Fact]
    public async Task ListArtifacts_ExistingAssetNoGenerations_Returns200EmptyArray()
    {
        var assetId = await _factory.SeedAssetAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);
        Assert.Empty(body!);
    }


    [Fact]
    public async Task ListArtifacts_OneGeneratedArtifact_ReturnsExactMetadata()
    {
        var assetId = await _factory.SeedAssetAsync("Test Asset");
        var generation = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "a test prompt"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);
        Assert.Single(body!);

        var entry = body![0];
        Assert.Equal(generation.ArtifactId, entry.ArtifactId);
        Assert.Equal(assetId.Value, entry.AssetId);
        Assert.Equal(generation.ArtifactName, entry.ArtifactName);
        Assert.Equal(generation.ArtifactType, entry.ArtifactType);
        Assert.Equal($"/api/artifacts/{generation.ArtifactId}/content", entry.ContentUrl);
    }


    [Fact]
    public async Task ListArtifacts_MultipleGenerationsSameAsset_ReturnsAllNewestFirst()
    {
        var assetId = await _factory.SeedAssetAsync("Gallery Asset");

        var gen1 = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "first prompt"
        });

        var gen2 = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "second prompt"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Count);
        Assert.Equal(gen2.ArtifactId, body[0].ArtifactId);
        Assert.Equal(gen1.ArtifactId, body[1].ArtifactId);
    }


    [Fact]
    public async Task ListArtifacts_AnotherAssetArtifacts_Excluded()
    {
        var assetA = await _factory.SeedAssetAsync("Asset A");
        var assetB = await _factory.SeedAssetAsync("Asset B");

        await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetA.Value,
            Prompt = "prompt for A"
        });

        await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetB.Value,
            Prompt = "prompt for B"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetA.Value}/artifacts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);
        Assert.Single(body!);
        Assert.Equal(assetA.Value, body![0].AssetId);
    }


    [Fact]
    public async Task ListArtifacts_ResponseContainsNoBase64OrProviderDetails()
    {
        var assetId = await _factory.SeedAssetAsync();
        await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "a test prompt"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("cloudflare", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flux", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("data:image", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content.bin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata.json", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountId", json, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task ListArtifacts_DoesNotCallExecutor()
    {
        _factory.Executor.Reset();
        var assetId = await _factory.SeedAssetAsync();

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        response.EnsureSuccessStatusCode();
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task ListArtifacts_EveryContentUrlRetrievable()
    {
        var assetId = await _factory.SeedAssetAsync("Download Test Asset");

        await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "first"
        });

        await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = assetId.Value,
            Prompt = "second"
        });

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{assetId.Value}/artifacts");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);

        foreach (var entry in body!)
        {
            var contentResponse = await client.GetAsync(entry.ContentUrl);
            Assert.Equal(HttpStatusCode.OK, contentResponse.StatusCode);
        }
    }


    [Fact]
    public async Task FullIterationLoop_SameAssetMultipleGenerations_ReturnsAllNewestFirst()
    {
        _factory.Executor.Reset();

        var createResponse = await _factory.CreateClient().PostAsJsonAsync(
            "/api/assets",
            new CreateAssetRequest { Name = "Iteration Asset", AssetType = "Weapon" });

        createResponse.EnsureSuccessStatusCode();
        var asset = await createResponse.Content.ReadFromJsonAsync<CreateAssetResponse>();
        Assert.NotNull(asset);

        var genX = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = asset!.AssetId,
            Prompt = "first generation"
        });

        var genY = await _factory.PostGenerationAsync(new GenerationRequest
        {
            AssetId = asset.AssetId,
            Prompt = "second generation"
        });

        Assert.NotEqual(genX.ArtifactId, genY.ArtifactId);
        Assert.Equal(2, _factory.Executor.CallCount);

        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/assets/{asset.AssetId}/artifacts");

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<List<ArtifactSummaryResponse>>();
        Assert.NotNull(body);
        Assert.Equal(2, body!.Count);

        Assert.Equal(genY.ArtifactId, body[0].ArtifactId);
        Assert.Equal(genX.ArtifactId, body[1].ArtifactId);

        var contentY = await client.GetAsync(body[0].ContentUrl);
        Assert.Equal(HttpStatusCode.OK, contentY.StatusCode);

        var contentX = await client.GetAsync(body[1].ContentUrl);
        Assert.Equal(HttpStatusCode.OK, contentX.StatusCode);
    }
}
