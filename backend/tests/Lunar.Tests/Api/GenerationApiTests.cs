using System.Net;
using System.Net.Http.Json;
using Lunar.Api.Contracts;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Lunar.Tests.Api;

public class GenerationApiTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public GenerationApiTests(LunarApiFactory factory)
    {
        _factory = factory;
        _factory.Executor.Reset();
    }


    [Fact]
    public async Task PostGeneration_WithValidRequest_Returns201AndArtifactMetadata()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "A ruined gothic watchtower under a blood-red eclipse"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GenerationResponse>();
        Assert.NotNull(body);
        Assert.Equal(assetId.Value, body!.AssetId);
        Assert.NotEqual(Guid.Empty, body.ArtifactId);
        Assert.NotEqual(Guid.Empty, body.WorkflowExecutionId);
        Assert.Equal("test-output.jpg", body.ArtifactName);
        Assert.Equal("ConceptImage", body.ArtifactType);
        Assert.Equal("image/jpeg", body.MediaType);
        Assert.Equal($"/api/artifacts/{body.ArtifactId}/content", body.ContentUrl);

        Assert.Equal(1, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_Success_ResponseDoesNotContainProviderOrStorageDetails()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "A test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("cloudflare", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("api.cloudflare.com", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flux-1-schnell", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiToken", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountId", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("content.bin", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("metadata.json", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".tmp-", json, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task PostGeneration_WithMissingAsset_Returns404()
    {
        var (_, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = Guid.NewGuid(),
            WorkflowDefinitionId = (await _factory.SeedWorkflowDefinitionAsync()).Id.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithMissingWorkflowDefinition_Returns404()
    {
        var assetId = await _factory.SeedAssetAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = Guid.NewGuid(),
            WorkflowDefinitionVersion = 1,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithMissingStep_Returns404AndNoExecutionSideEffects()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();
        _factory.ExecutionRepository.Reset();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 99,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("workflow_step_not_found", body!.Code);

        Assert.Equal(0, _factory.Executor.CallCount);
        Assert.Equal(0, _factory.ExecutionRepository.TryAddCallCount);
        Assert.Equal(0, _factory.ExecutionRepository.TryUpdateCallCount);
    }


    [Fact]
    public async Task PostGeneration_WithRateLimitedFailure_Returns429AndRetryAfter()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        _factory.Executor.Failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.RateLimited,
            TimeSpan.FromSeconds(30));

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);

        var retryAfterHeader = response.Headers.TryGetValues("Retry-After", out var retryAfterValues);
        Assert.True(retryAfterHeader, "Retry-After header must be present for rate-limited responses.");
        Assert.Equal("30", retryAfterValues!.First());

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("rate_limited", body!.Code);
        Assert.Equal(30, body.RetryAfterSeconds);
    }


    [Fact]
    public async Task PostGeneration_WithQuotaExhausted_Returns503Not401Or403()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        _factory.Executor.Failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.QuotaExhausted);

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("quota_exhausted", body!.Code);
    }


    [Fact]
    public async Task PostGeneration_WithInvalidResponse_Returns502()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        _factory.Executor.Failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.InvalidResponse);

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(body);
        Assert.Equal("invalid_response", body!.Code);
    }


    [Fact]
    public async Task PostGeneration_WithEmptyAssetId_Returns400()
    {
        var (_, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = Guid.Empty,
            WorkflowDefinitionId = (await _factory.SeedWorkflowDefinitionAsync()).Id.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithEmptyWorkflowDefinitionId_Returns400()
    {
        var assetId = await _factory.SeedAssetAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = Guid.Empty,
            WorkflowDefinitionVersion = 1,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithVersionZero_Returns400()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, _) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = 0,
            StepPosition = 1,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithStepZero_Returns400()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 0,
            Prompt = "test prompt"
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }


    [Fact]
    public async Task PostGeneration_WithBlankPrompt_Returns400()
    {
        var assetId = await _factory.SeedAssetAsync();
        var (definitionId, version) = await _factory.SeedWorkflowDefinitionAsync();

        var request = new GenerationRequest
        {
            AssetId = assetId.Value,
            WorkflowDefinitionId = definitionId.Value,
            WorkflowDefinitionVersion = version,
            StepPosition = 1,
            Prompt = "   "
        };

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, _factory.Executor.CallCount);
    }
}
