using System.Net;
using System.Net.Http.Json;
using System.Text;
using Lunar.Api.Contracts;
using Lunar.Core.Assets;

namespace Lunar.Tests.Api;

public class AssetApiTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public AssetApiTests(LunarApiFactory factory)
    {
        _factory = factory;
    }


    [Fact]
    public async Task PostAsset_WithValidRequest_Returns201AndAssetMetadata()
    {
        var request = new CreateAssetRequest
        {
            Name = "Ruined Gothic Watchtower",
            AssetType = "Environment"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAssetResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.AssetId);
        Assert.Equal("Ruined Gothic Watchtower", body.Name);
        Assert.Equal("Environment", body.AssetType);
    }


    [Fact]
    public async Task PostAsset_WithBrowserEquivalentRawJson_Returns201AndExactMetadata()
    {
        var json = """{"name":"Ruined Gothic Watchtower","assetType":"Environment"}""";

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<CreateAssetResponse>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.AssetId);
        Assert.Equal("Ruined Gothic Watchtower", body.Name);
        Assert.Equal("Environment", body.AssetType);
    }


    [Fact]
    public async Task PostAsset_WithBrowserEquivalentRawJson_CreatedAssetRemainsUsableForGeneration()
    {
        var json = """{"name":"Proxy Smoke Asset","assetType":"Environment"}""";

        var client = _factory.CreateClient();
        var createResponse = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        createResponse.EnsureSuccessStatusCode();

        var asset = await createResponse.Content.ReadFromJsonAsync<CreateAssetResponse>();
        Assert.NotNull(asset);

        var generationRequest = new GenerationRequest
        {
            AssetId = asset!.AssetId,
            Prompt = "a test prompt"
        };

        var generationResponse = await client.PostAsJsonAsync("/api/generations", generationRequest);
        Assert.Equal(HttpStatusCode.Created, generationResponse.StatusCode);
    }


    [Fact]
    public async Task PostAsset_Success_ExactNameRoundTrips()
    {
        var name = "  My Special Asset  ";
        var request = new CreateAssetRequest
        {
            Name = name,
            AssetType = "Character"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", request);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<CreateAssetResponse>();
        Assert.NotNull(body);
        Assert.Equal(name, body!.Name);
    }


    [Fact]
    public async Task PostAsset_Success_ExactAssetTypeRoundTripsForEachValidName()
    {
        foreach (var typeName in Enum.GetNames<AssetType>())
        {
            var json = $$"""{"name":"Asset {{typeName}}","assetType":"{{typeName}}"}""";

            var client = _factory.CreateClient();
            var response = await client.PostAsync(
                "/api/assets",
                new StringContent(json, Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<CreateAssetResponse>();
            Assert.NotNull(body);
            Assert.Equal(typeName, body!.AssetType);
        }
    }


    [Fact]
    public async Task PostAsset_WithBlankName_Returns400()
    {
        var request = new CreateAssetRequest
        {
            Name = "   ",
            AssetType = "Character"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_name", body!.Code);
    }


    [Fact]
    public async Task PostAsset_WithUnknownAssetTypeString_Returns400InvalidAssetType()
    {
        var json = """{"name":"Test Asset","assetType":"NotARealAssetType"}""";

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_asset_type", body!.Code);
    }


    [Fact]
    public async Task PostAsset_WithNumericStringAssetType_Returns400InvalidAssetType()
    {
        var json = """{"name":"Test Asset","assetType":"1"}""";

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_asset_type", body!.Code);
    }


    [Fact]
    public async Task PostAsset_WithNumericAssetType_Returns400InvalidAssetType()
    {
        var json = """{"name":"Test Asset","assetType":1}""";

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task PostAsset_WithNullAssetType_Returns400InvalidAssetType()
    {
        var json = """{"name":"Test Asset","assetType":null}""";

        var client = _factory.CreateClient();
        var response = await client.PostAsync(
            "/api/assets",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task PostAsset_Success_ResponseDoesNotContainProviderOrStorageDetails()
    {
        var request = new CreateAssetRequest
        {
            Name = "Test Asset",
            AssetType = "Prop"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("cloudflare", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flux", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content.bin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata.json", json, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task PostAsset_Success_DoesNotCallExecutor()
    {
        _factory.Executor.Reset();

        var request = new CreateAssetRequest
        {
            Name = "Test Asset",
            AssetType = "Character"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/assets", request);
        response.EnsureSuccessStatusCode();

        Assert.Equal(0, _factory.Executor.CallCount);
    }
}
