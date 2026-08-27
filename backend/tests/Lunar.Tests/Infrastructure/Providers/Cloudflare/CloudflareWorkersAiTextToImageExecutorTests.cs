using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Infrastructure.Providers.Cloudflare;

public class CloudflareWorkersAiTextToImageExecutorTests
{
    private const string TestAccountId = "test-account";
    private const string TestApiToken = "test-token";
    private const string TestBaseAddress = "https://api.cloudflare.com/";
    private const string TestModelId = "@cf/black-forest-labs/flux-1-schnell";

    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };

    private static CloudflareWorkersAiOptions ValidOptions(TimeSpan? timeout = null, int? steps = null) =>
        new()
        {
            BaseAddress = TestBaseAddress,
            AccountId = TestAccountId,
            ApiToken = TestApiToken,
            RequestTimeout = timeout ?? TimeSpan.FromSeconds(60),
            TextToImageModelId = TestModelId,
            TextToImageSteps = steps ?? 4
        };

    private static CloudflareWorkersAiConfiguration ValidConfiguration(
        TimeSpan? timeout = null,
        int? steps = null) =>
        CloudflareWorkersAiConfiguration.From(ValidOptions(timeout, steps));

    private static CloudflareWorkersAiClient CreateClient(
        FakeHttpMessageHandler handler,
        CloudflareWorkersAiConfiguration? configuration = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(TestBaseAddress),
            Timeout = Timeout.InfiniteTimeSpan
        };

        return new CloudflareWorkersAiClient(httpClient, configuration ?? ValidConfiguration());
    }

    private static CloudflareWorkersAiTextToImageExecutor CreateExecutor(
        FakeHttpMessageHandler handler,
        CloudflareWorkersAiConfiguration? configuration = null)
    {
        return new CloudflareWorkersAiTextToImageExecutor(CreateClient(handler, configuration), NullLogger<CloudflareWorkersAiTextToImageExecutor>.Instance);
    }

    private static CapabilityExecutionRequest CreateRequest(string prompt) =>
        new(
            CapabilityId.New(),
            AssetId.New(),
            WorkflowExecutionId.New(),
            WorkflowDefinitionId.New(),
            1,
            1,
            new TextPromptInput(prompt));


    // ---- Request tests ----

    [Fact]
    public async Task ExecuteAsync_Success_ShouldSendPostToCorrectRouteWithBearerTokenAndJsonBody()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        await CreateExecutor(handler).ExecuteAsync(CreateRequest("a dark fantasy raven shrine"));

        Assert.Single(handler.CapturedRequests);
        var captured = handler.GetCapturedRequest(0);

        Assert.Equal(HttpMethod.Post, captured.Method);
        Assert.Equal(
            $"/client/v4/accounts/{TestAccountId}/ai/run/{TestModelId}",
            new Uri(captured.RequestUri!).AbsolutePath);
        Assert.Equal("Bearer", captured.AuthScheme);
        Assert.Equal(TestApiToken, captured.AuthParameter);
        Assert.Equal("application/json", captured.ContentType);

        var body = JsonSerializer.Deserialize<JsonElement>(captured.Body!);
        Assert.Equal("a dark fantasy raven shrine", body.GetProperty("prompt").GetString());
        Assert.Equal(4, body.GetProperty("steps").GetInt32());
        Assert.False(body.TryGetProperty("seed", out _));
        Assert.False(body.TryGetProperty("width", out _));
        Assert.False(body.TryGetProperty("height", out _));
        Assert.False(body.TryGetProperty("negative_prompt", out _));
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPreserveExactPromptCasingAndSpacing()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        var prompt = "  Ancient  RAVEN Shrine, Moonlit -- Cracked Stone  ";
        await CreateExecutor(handler).ExecuteAsync(CreateRequest(prompt));

        var body = JsonSerializer.Deserialize<JsonElement>(handler.GetCapturedRequest(0).Body!);
        Assert.Equal(prompt, body.GetProperty("prompt").GetString());
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldUseHttpsAndConfiguredBaseAddress()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        await CreateExecutor(handler).ExecuteAsync(CreateRequest("test prompt"));

        Assert.Single(handler.CapturedRequests);
        var captured = handler.GetCapturedRequest(0);
        Assert.StartsWith("https://", captured.RequestUri!);
        Assert.Contains("api.cloudflare.com", captured.RequestUri);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldUseConfiguredStepsValue()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        await CreateExecutor(handler, ValidConfiguration(steps: 5))
            .ExecuteAsync(CreateRequest("test prompt"));

        var body = JsonSerializer.Deserialize<JsonElement>(handler.GetCapturedRequest(0).Body!);
        Assert.Equal(5, body.GetProperty("steps").GetInt32());
    }


    // ---- Success response tests ----

    [Fact]
    public async Task ExecuteAsync_Success_ShouldReturnSucceededWithDecodedJpegBytes()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test prompt"));

        var succeeded = Assert.IsType<CapabilityExecutionSucceeded>(outcome);
        var content = Assert.IsType<BinaryArtifactContent>(succeeded.Output.Content);
        Assert.Equal("image/jpeg", content.MediaType);
        Assert.Equal(JpegBytes, content.Data.ToArray());
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldUseGeneratedImageNameAndConceptImageType()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test prompt"));

        var succeeded = Assert.IsType<CapabilityExecutionSucceeded>(outcome);
        Assert.Equal("Generated image", succeeded.Output.ArtifactName);
        Assert.Equal(ArtifactType.ConceptImage, succeeded.Output.ArtifactType);
        Assert.Empty(succeeded.Output.SourceArtifactIds);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldMakeExactlyOneRequest()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        await CreateExecutor(handler).ExecuteAsync(CreateRequest("test prompt"));

        Assert.Single(handler.CapturedRequests);
    }


    // ---- HTTP 2xx + success:false tests ----

    [Fact]
    public async Task ExecuteAsync_Http200WithSuccessFalseAndCode3036_ShouldMapToQuotaExhausted()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.OK, false, 3036));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http200WithSuccessFalseAndCode3040_ShouldMapToTemporarilyUnavailable()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.OK, false, 3040));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http200WithSuccessFalseAndCode5035_ShouldMapToPaidPlanRequired()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.OK, false, 5035));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.PaidPlanRequired, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http200WithSuccessFalseAndNoErrors_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.OK, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Provider error mapping tests ----

    [Fact]
    public async Task ExecuteAsync_Error3036WithHttp429_ShouldMapToQuotaExhausted()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, 3036));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Error3040WithHttp429_ShouldMapToTemporarilyUnavailable()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, 3040));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Error5035WithHttp403_ShouldMapToPaidPlanRequired()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.Forbidden, false, 5035));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.PaidPlanRequired, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Theory]
    [InlineData(3007)]
    [InlineData(3008)]
    public async Task ExecuteAsync_TimeoutCodesWithHttp408_ShouldMapToTimedOut(int code)
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.RequestTimeout, false, code));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TimedOut, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_KnownCodeNotFirstInErrors_ShouldStillMapCorrectly()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponseWithMultipleCodes(
                HttpStatusCode.TooManyRequests,
                9999,
                3036));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http401_ShouldMapToAuthenticationFailed()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.Unauthorized, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.AuthenticationFailed, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http403_ShouldMapToAccessDenied()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.Forbidden, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.AccessDenied, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http408_ShouldMapToTimedOut()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.RequestTimeout, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TimedOut, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http429_ShouldMapToRateLimited()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    public async Task ExecuteAsync_Generic4xx_ShouldMapToRejected(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(status, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.Rejected, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task ExecuteAsync_Generic5xx_ShouldMapToTemporarilyUnavailable(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(status, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Redirect tests ----

    [Fact]
    public async Task ExecuteAsync_Http307_ShouldNotFollowRedirectAndMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TemporaryRedirect)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Http308_ShouldNotFollowRedirectAndMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.PermanentRedirect)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Retry-After tests ----

    [Fact]
    public async Task ExecuteAsync_RetryAfterDeltaSeconds_ShouldBePreserved()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null);
            response.Headers.Add("Retry-After", "30");
            return response;
        });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);
        Assert.Equal(TimeSpan.FromSeconds(30), failed.Failure.RetryAfter);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_MissingRetryAfter_ShouldBeNull()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Null(failed.Failure.RetryAfter);
    }


    [Fact]
    public async Task ExecuteAsync_ZeroRetryAfter_ShouldBeTreatedAsAbsent()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null);
            response.Headers.Add("Retry-After", "0");
            return response;
        });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Null(failed.Failure.RetryAfter);
    }


    // ---- Invalid response tests ----

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("not json", Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_MissingSuccessField_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"result\":{\"image\":\"" + Convert.ToBase64String(JpegBytes) + "\"}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_SuccessTrueWithMissingResult_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_SuccessTrueWithMissingImage_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"result\":{}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_SuccessTrueWithNullImage_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"result\":{\"image\":null}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_SuccessTrueWithEmptyImage_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"result\":{\"image\":\"\"}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_SuccessTrueWithWhitespaceImage_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"result\":{\"image\":\"   \"}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_InvalidBase64_ShouldMapToInvalidResponse()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"result\":{\"image\":\"not!!!valid!!!base64\"}}",
                    Encoding.UTF8, "application/json")
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_DecodedBytesWithoutJpegSignature_ShouldMapToInvalidResponse()
    {
        var nonJpegBytes = new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(nonJpegBytes));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Transport uncertainty tests ----

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_ShouldMapToRemoteOutcomeUnknown()
    {
        var handler = new FakeHttpMessageHandler(
            _ => throw new HttpRequestException("connection refused"));

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_LocalTimeout_ShouldMapToRemoteOutcomeUnknown()
    {
        var handler = new FakeHttpMessageHandler(
            async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return CreateSuccessResponse(JpegBytes);
            });

        var outcome = await CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromMilliseconds(50)))
            .ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Caller cancellation tests ----

    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_ShouldPropagateOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        var executor = CreateExecutor(handler);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(CreateRequest("test"), cts.Token));

        Assert.Empty(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_CallerCancellationDuringSend_ShouldPropagateOperationCanceledException()
    {
        var handler = new FakeHttpMessageHandler(
            async (_, ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
                return CreateSuccessResponse(JpegBytes);
            });

        var executor = CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromSeconds(60)));

        var cts = new CancellationTokenSource();
        var invokeTask = executor.ExecuteAsync(CreateRequest("test"), cts.Token);

        await Task.Delay(50);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Response-body cancellation tests ----

    [Fact]
    public async Task ExecuteAsync_CallerCancelsDuringSuccessBodyRead_ShouldPropagateOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        var blockingContent = new BlockingHttpContent();

        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = blockingContent
            });

        var executor = CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromSeconds(60)));

        var invokeTask = executor.ExecuteAsync(CreateRequest("test"), cts.Token);

        await blockingContent.HeadersDelivered.Task;

        cts.Cancel();
        blockingContent.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_LocalTimeoutDuringSuccessBodyRead_ShouldMapToRemoteOutcomeUnknown()
    {
        var blockingContent = new BlockingHttpContent();

        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = blockingContent
            });

        var executor = CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromMilliseconds(50)));

        var invokeTask = executor.ExecuteAsync(CreateRequest("test"), CancellationToken.None);

        await blockingContent.HeadersDelivered.Task;

        var outcome = await invokeTask;
        blockingContent.Release();

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_CallerCancelsDuringErrorBodyRead_ShouldPropagateOperationCanceledException()
    {
        var cts = new CancellationTokenSource();
        var blockingContent = new BlockingHttpContent();

        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = blockingContent
            });

        var executor = CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromSeconds(60)));

        var invokeTask = executor.ExecuteAsync(CreateRequest("test"), cts.Token);

        await blockingContent.HeadersDelivered.Task;

        cts.Cancel();
        blockingContent.Release();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => invokeTask);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_LocalTimeoutDuringErrorBodyRead_ShouldFallBackToHttpStatusClassification()
    {
        var blockingContent = new BlockingHttpContent();

        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = blockingContent
            });

        var executor = CreateExecutor(handler, ValidConfiguration(timeout: TimeSpan.FromMilliseconds(50)));

        var invokeTask = executor.ExecuteAsync(CreateRequest("test"), CancellationToken.None);

        await blockingContent.HeadersDelivered.Task;

        var outcome = await invokeTask;
        blockingContent.Release();

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Response-body transport failure tests ----

    [Fact]
    public async Task ExecuteAsync_SuccessBodyTransportFailure_ShouldMapToRemoteOutcomeUnknown()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ThrowingHttpContent()
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_ErrorBodyTransportFailure_ShouldFallBackToHttpStatusClassification()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new ThrowingHttpContent()
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    [Fact]
    public async Task ExecuteAsync_Error503BodyTransportFailure_ShouldFallBackToTemporarilyUnavailable()
    {
        var handler = new FakeHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new ThrowingHttpContent()
            });

        var outcome = await CreateExecutor(handler).ExecuteAsync(CreateRequest("test"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
        Assert.Single(handler.CapturedRequests);
    }


    // ---- Unsupported input test ----

    [Fact]
    public async Task ExecuteAsync_UnsupportedInput_ShouldThrowWithoutHttpCall()
    {
        var handler = new FakeHttpMessageHandler(
            _ => CreateSuccessResponse(JpegBytes));

        var executor = CreateExecutor(handler);

        var request = new CapabilityExecutionRequest(
            CapabilityId.New(),
            AssetId.New(),
            WorkflowExecutionId.New(),
            WorkflowDefinitionId.New(),
            1,
            1,
            new UnsupportedTestInput());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            executor.ExecuteAsync(request));

        Assert.Empty(handler.CapturedRequests);
    }


    // ---- Concurrency test ----

    [Fact]
    public async Task ExecuteAsync_ConcurrentCalls_ShouldProduceExactlyCorrelatedOutputs()
    {
        var promptA = "dark fantasy raven shrine";
        var promptB = "bright sci-fi space station";

        var bytesA = new byte[] { 0xFF, 0xD8, 0xFF, 0xAA };
        var bytesB = new byte[] { 0xFF, 0xD8, 0xFF, 0xBB };

        var handler = new FakeHttpMessageHandler(
            async (req, ct) =>
            {
                var body = await req.Content!.ReadAsStringAsync(ct);
                var json = JsonSerializer.Deserialize<JsonElement>(body);
                var prompt = json.GetProperty("prompt").GetString();

                await Task.Yield();

                return prompt == promptA
                    ? CreateSuccessResponse(bytesA)
                    : CreateSuccessResponse(bytesB);
            });

        var executor = CreateExecutor(handler);

        var taskA = executor.ExecuteAsync(CreateRequest(promptA));
        var taskB = executor.ExecuteAsync(CreateRequest(promptB));

        var results = await Task.WhenAll(taskA, taskB);

        Assert.Equal(2, handler.CapturedRequests.Count);

        var succeededA = Assert.IsType<CapabilityExecutionSucceeded>(results[0]);
        var succeededB = Assert.IsType<CapabilityExecutionSucceeded>(results[1]);

        var contentA = Assert.IsType<BinaryArtifactContent>(succeededA.Output.Content);
        var contentB = Assert.IsType<BinaryArtifactContent>(succeededB.Output.Content);

        Assert.Equal(bytesA, contentA.Data.ToArray());
        Assert.Equal(bytesB, contentB.Data.ToArray());
    }


    // ---- Configuration validation tests ----

    [Fact]
    public void Configuration_EmptyBaseAddress_ShouldThrow()
    {
        var options = ValidOptions();
        options.BaseAddress = "";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_WhitespaceBaseAddress_ShouldThrow()
    {
        var options = ValidOptions();
        options.BaseAddress = "   ";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_NonAbsoluteBaseAddress_ShouldThrow()
    {
        var options = ValidOptions();
        options.BaseAddress = "not-a-uri";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_HttpBaseAddress_ShouldThrow()
    {
        var options = ValidOptions();
        options.BaseAddress = "http://api.cloudflare.com/";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_EmptyAccountId_ShouldThrow()
    {
        var options = ValidOptions();
        options.AccountId = "";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_WhitespaceAccountId_ShouldThrow()
    {
        var options = ValidOptions();
        options.AccountId = "   ";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_EmptyApiToken_ShouldThrow()
    {
        var options = ValidOptions();
        options.ApiToken = "";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_WhitespaceApiToken_ShouldThrow()
    {
        var options = ValidOptions();
        options.ApiToken = "   ";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_ZeroTimeout_ShouldThrow()
    {
        var options = ValidOptions();
        options.RequestTimeout = TimeSpan.Zero;

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_NegativeTimeout_ShouldThrow()
    {
        var options = ValidOptions();
        options.RequestTimeout = TimeSpan.FromSeconds(-1);

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_EmptyModelId_ShouldThrow()
    {
        var options = ValidOptions();
        options.TextToImageModelId = "";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_WhitespaceModelId_ShouldThrow()
    {
        var options = ValidOptions();
        options.TextToImageModelId = "   ";

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_StepsZero_ShouldThrow()
    {
        var options = ValidOptions();
        options.TextToImageSteps = 0;

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_StepsAboveMaximum_ShouldThrow()
    {
        var options = ValidOptions();
        options.TextToImageSteps = 9;

        Assert.Throws<InvalidOperationException>(() =>
            CloudflareWorkersAiConfiguration.From(options));
    }


    [Fact]
    public void Configuration_NullOptions_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CloudflareWorkersAiConfiguration.From(null!));
    }


    [Fact]
    public void Configuration_ToString_ShouldNotRevealApiToken()
    {
        var config = ValidConfiguration();

        var toStringResult = config.ToString();

        Assert.DoesNotContain(TestApiToken, toStringResult);
    }


    [Fact]
    public void Configuration_ShouldProduceImmutableSnapshot()
    {
        var options = ValidOptions();
        var config = CloudflareWorkersAiConfiguration.From(options);

        options.ApiToken = "mutated-value";

        Assert.Equal(TestApiToken, config.ApiToken);
    }


    [Fact]
    public void Configuration_ValidBaseAddress_ShouldPreserveExactHttpsUri()
    {
        var config = ValidConfiguration();

        Assert.Equal(TestBaseAddress, config.BaseAddress.ToString());
        Assert.True(config.BaseAddress.IsAbsoluteUri);
        Assert.Equal("https", config.BaseAddress.Scheme);
    }


    [Fact]
    public void Client_NullHttpClient_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareWorkersAiClient(null!, ValidConfiguration()));
    }


    [Fact]
    public void Client_NullConfiguration_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareWorkersAiClient(new HttpClient(), null!));
    }


    [Fact]
    public void Executor_NullClient_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CloudflareWorkersAiTextToImageExecutor(null!, NullLogger<CloudflareWorkersAiTextToImageExecutor>.Instance));
    }


    // ---- Helpers ----

    private static HttpResponseMessage CreateSuccessResponse(byte[] imageBytes)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        var json = $"{{\"success\":true,\"result\":{{\"image\":\"{base64}\"}}}}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateEnvelopeResponse(
        HttpStatusCode status,
        bool? success,
        int? errorCode)
    {
        var successPart = success.HasValue
            ? $"\"success\":{success.Value.ToString().ToLowerInvariant()},"
            : "";

        var errorsPart = errorCode is { } code
            ? $"\"errors\":[{{\"code\":{code},\"message\":\"error\"}}]"
            : "\"errors\":[]";

        var json = $"{{{successPart}{errorsPart}}}";
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CreateEnvelopeResponseWithMultipleCodes(
        HttpStatusCode status,
        params int[] errorCodes)
    {
        var errors = string.Join(",", errorCodes.Select(c =>
            $"{{\"code\":{c},\"message\":\"error\"}}"));
        var json = $"{{\"success\":false,\"errors\":[{errors}]}}";
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }


    private sealed record UnsupportedTestInput : CapabilityExecutionInput;


    private sealed record CapturedHttpRequest
    {
        public required HttpMethod Method { get; init; }
        public required string? RequestUri { get; init; }
        public required string? AuthScheme { get; init; }
        public required string? AuthParameter { get; init; }
        public required string? ContentType { get; init; }
        public required string? Body { get; init; }
    }


    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;
        private int _callCount;

        public int CallCount => _callCount;

        public ConcurrentQueue<CapturedHttpRequest> CapturedRequests { get; } = new();


        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = (req, ct) => Task.FromResult(responder(req));
        }

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }


        public CapturedHttpRequest GetCapturedRequest(int index) =>
            CapturedRequests.ElementAt(index);


        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _callCount);

            string? body = null;
            string? contentType = null;

            if (request.Content is not null)
            {
                body = await request.Content.ReadAsStringAsync(cancellationToken);
                contentType = request.Content.Headers.ContentType?.MediaType;
            }

            var authScheme = request.Headers.Authorization?.Scheme;
            var authParameter = request.Headers.Authorization?.Parameter;

            CapturedRequests.Enqueue(new CapturedHttpRequest
            {
                Method = request.Method,
                RequestUri = request.RequestUri?.ToString(),
                AuthScheme = authScheme,
                AuthParameter = authParameter,
                ContentType = contentType,
                Body = body
            });

            return await _responder(request, cancellationToken);
        }
    }


    /// <summary>
    /// Test-only HttpContent backed by a stream whose ReadAsync blocks
    /// until released or the cancellation token fires.
    /// </summary>
    private sealed class BlockingHttpContent : HttpContent
    {
        private readonly BlockingReadStream _stream;

        public TaskCompletionSource HeadersDelivered => _stream.ReadStarted;

        public BlockingHttpContent()
        {
            _stream = new BlockingReadStream();
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }


        public void Release() => _stream.Release();


        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(_stream);
        }


        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            _stream.CopyTo(stream);
            return Task.CompletedTask;
        }


        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }


        private sealed class BlockingReadStream : Stream
        {
            private readonly TaskCompletionSource _readStarted;
            private readonly TaskCompletionSource _releaseTcs;

            public TaskCompletionSource ReadStarted => _readStarted;

            public BlockingReadStream()
            {
                _readStarted = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _releaseTcs = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }


            public void Release() => _releaseTcs.TrySetResult();


            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }


            public override async Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                _readStarted.TrySetResult();

                await _releaseTcs.Task.WaitAsync(cancellationToken);

                return 0;
            }


            public override int Read(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();


            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();
            public override void SetLength(long value) =>
                throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();
        }
    }


    /// <summary>
    /// Test-only HttpContent that throws IOException when the body is read,
    /// simulating a transport failure after headers are received.
    /// </summary>
    private sealed class ThrowingHttpContent : HttpContent
    {
        public ThrowingHttpContent()
        {
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }


        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(new ThrowingStream());
        }


        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            throw new IOException("connection reset");
        }


        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }


        private sealed class ThrowingStream : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }


            public override Task<int> ReadAsync(
                byte[] buffer,
                int offset,
                int count,
                CancellationToken cancellationToken)
            {
                throw new IOException("connection reset during body read");
            }


            public override int Read(byte[] buffer, int offset, int count) =>
                throw new IOException("connection reset during body read");


            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();
            public override void SetLength(long value) =>
                throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();
        }
    }
}
