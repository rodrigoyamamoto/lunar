using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Lunar.Application;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Telemetry;

[Collection("Telemetry")]
public class ProviderTelemetryTests
{
    private const string TestAccountId = "test-account";
    private const string TestApiToken = "test-token";
    private const string TestBaseAddress = "https://api.cloudflare.com/";
    private const string TestModelId = "@cf/black-forest-labs/flux-1-schnell";

    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };


    [Fact]
    public async Task RealProvider_Success_LogsProviderCompletedWithBoundedTags()
    {
        var provider = new CaptureLoggerProvider();
        var handler = new FakeHttpMessageHandler(_ => CreateSuccessResponse(JpegBytes));
        var executor = CreateExecutor(handler, provider);

        await executor.ExecuteAsync(CreateRequest("test prompt"));

        var completedEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information && e.Message.Contains("Provider generation completed"));
        Assert.NotNull(completedEntry);
        Assert.Contains(InfrastructureTelemetry.ProviderCloudflareWorkersAi, completedEntry.Message);
        Assert.Contains(TestModelId, completedEntry.Message);

        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        Assert.DoesNotContain(TestAccountId, allMessages);
        Assert.DoesNotContain(TestApiToken, allMessages);
        Assert.DoesNotContain("accounts/", allMessages);
        Assert.DoesNotContain("api.cloudflare.com", allMessages);
        Assert.DoesNotContain("Bearer", allMessages);
    }


    [Fact]
    public async Task RealProvider_Success_DoesNotClaimFakeUsage()
    {
        // Cloudflare Workers AI image generation API does not return per-request
        // Neuron usage. The provider log must not fabricate usage data.
        var provider = new CaptureLoggerProvider();
        var handler = new FakeHttpMessageHandler(_ => CreateSuccessResponse(JpegBytes));
        var executor = CreateExecutor(handler, provider);

        await executor.ExecuteAsync(CreateRequest("test prompt"));

        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n", provider.Entries
            .SelectMany(e => e.Properties.Values)
            .Select(v => v?.ToString() ?? string.Empty));
        var combined = allMessages + "\n" + allProperties;

        Assert.DoesNotContain("CreditsSpent", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ActualCost", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ActualNeurons", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UsageNeurons", combined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("EstimatedNeurons", combined, StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task RealProvider_Success_EmitsProviderActivityWithOkStatus()
    {
        using var listener = new TestActivityListener(
            ApplicationTelemetry.ActivitySourceName,
            InfrastructureTelemetry.ActivitySourceName);
        var handler = new FakeHttpMessageHandler(_ => CreateSuccessResponse(JpegBytes));
        var executor = CreateExecutor(handler);

        await executor.ExecuteAsync(CreateRequest("test prompt"));

        var providerActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == InfrastructureTelemetry.ProviderRequestActivityName
                 && a.GetTagItem(InfrastructureTelemetry.ProviderNameTag)?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi
                 && a.GetTagItem(InfrastructureTelemetry.ProviderModelTag)?.ToString() == TestModelId
                 && a.Status == ActivityStatusCode.Ok);
        Assert.NotNull(providerActivity);
        Assert.Equal(InfrastructureTelemetry.ProviderCloudflareWorkersAi,
            providerActivity.GetTagItem(InfrastructureTelemetry.ProviderNameTag)?.ToString());
        Assert.Equal(TestModelId,
            providerActivity.GetTagItem(InfrastructureTelemetry.ProviderModelTag)?.ToString());
        Assert.Equal(InfrastructureTelemetry.OutcomeSuccess,
            providerActivity.GetTagItem(InfrastructureTelemetry.OperationOutcomeTag)?.ToString());

        // No sensitive data in activity tags
        var allTags = providerActivity.Tags.Select(t => t.Value?.ToString() ?? string.Empty);
        var allTagValues = string.Join("\n", allTags);
        Assert.DoesNotContain(TestAccountId, allTagValues);
        Assert.DoesNotContain(TestApiToken, allTagValues);
        Assert.DoesNotContain("accounts/", allTagValues);
        Assert.DoesNotContain("api.cloudflare.com", allTagValues);
    }


    [Fact]
    public async Task RealProvider_Success_EmitsProviderMetrics()
    {
        using var meterListener = new TestMeterListener(
            ApplicationTelemetry.MeterName,
            InfrastructureTelemetry.MeterName);
        var handler = new FakeHttpMessageHandler(_ => CreateSuccessResponse(JpegBytes));
        var executor = CreateExecutor(handler);

        await executor.ExecuteAsync(CreateRequest("test prompt"));

        var requests = meterListener.GetCounterValues("lunar.provider.requests");
        Assert.NotEmpty(requests);

        var workerAiRequests = requests.Where(r =>
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderNameMetricTag, out var provider) &&
            provider?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi &&
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderModelMetricTag, out var model) &&
            model?.ToString() == TestModelId).ToList();
        Assert.NotEmpty(workerAiRequests);
        Assert.Equal(1L, (long)workerAiRequests.Sum(r => r.Value));

        var durations = meterListener.GetHistogramValues("lunar.provider.request.duration");
        Assert.NotEmpty(durations);

        var workerAiDurations = durations.Where(r =>
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderNameMetricTag, out var dProvider) &&
            dProvider?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi &&
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderModelMetricTag, out var dModel) &&
            dModel?.ToString() == TestModelId).ToList();
        Assert.NotEmpty(workerAiDurations);

        var outputSizes = meterListener.GetHistogramValues("lunar.provider.output.size");
        Assert.NotEmpty(outputSizes);

        var workerAiOutputSizes = outputSizes.Where(r =>
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderNameMetricTag, out var sProvider) &&
            sProvider?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi &&
            r.Tags.TryGetValue(InfrastructureTelemetry.ProviderModelMetricTag, out var sModel) &&
            sModel?.ToString() == TestModelId).ToList();
        Assert.NotEmpty(workerAiOutputSizes);
        Assert.True(workerAiOutputSizes.All(r => r.Value > 0));

        // Verify bounded tags
        foreach (var record in workerAiRequests)
        {
            Assert.True(record.Tags.TryGetValue(InfrastructureTelemetry.ProviderNameMetricTag, out var boundedProvider));
            Assert.Equal(InfrastructureTelemetry.ProviderCloudflareWorkersAi, boundedProvider?.ToString());
            Assert.True(record.Tags.TryGetValue(InfrastructureTelemetry.ProviderModelMetricTag, out var boundedModel));
            Assert.Equal(TestModelId, boundedModel?.ToString());
        }
    }


    [Fact]
    public async Task RealProvider_ExpectedFailure_LogsWarningWithFailureKind()
    {
        var provider = new CaptureLoggerProvider();
        var handler = new FakeHttpMessageHandler(_ =>
            CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null));
        var executor = CreateExecutor(handler, provider);

        var outcome = await executor.ExecuteAsync(CreateRequest("test prompt"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);

        var warningEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Provider generation failed"));
        Assert.NotNull(warningEntry);
        Assert.Contains(InfrastructureTelemetry.ProviderCloudflareWorkersAi, warningEntry.Message);
        Assert.Contains(TestModelId, warningEntry.Message);

        var errorEntries = provider.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Empty(errorEntries);

        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        Assert.DoesNotContain(TestAccountId, allMessages);
        Assert.DoesNotContain(TestApiToken, allMessages);
        Assert.DoesNotContain("accounts/", allMessages);
        Assert.DoesNotContain("api.cloudflare.com", allMessages);
    }


    [Fact]
    public async Task RealProvider_ExpectedFailure_EmitsFailureActivity()
    {
        using var listener = new TestActivityListener(
            ApplicationTelemetry.ActivitySourceName,
            InfrastructureTelemetry.ActivitySourceName);
        var handler = new FakeHttpMessageHandler(_ =>
            CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null));
        var executor = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest("test prompt"));

        Assert.True(outcome is CapabilityExecutionFailed);

        var providerActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == InfrastructureTelemetry.ProviderRequestActivityName
                 && a.GetTagItem(InfrastructureTelemetry.ProviderNameTag)?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi
                 && a.GetTagItem(InfrastructureTelemetry.ProviderModelTag)?.ToString() == TestModelId
                 && a.Status == ActivityStatusCode.Error);
        Assert.NotNull(providerActivity);
        Assert.Equal(ActivityStatusCode.Error, providerActivity!.Status);
        Assert.Equal(InfrastructureTelemetry.OutcomeFailure,
            providerActivity.GetTagItem(InfrastructureTelemetry.OperationOutcomeTag)?.ToString());

        var failureKind = providerActivity.GetTagItem(InfrastructureTelemetry.FailureKindTag)?.ToString();
        Assert.NotNull(failureKind);
        Assert.False(string.IsNullOrEmpty(failureKind));
    }


    [Fact]
    public async Task RealProvider_ExpectedFailure_EmitsFailureMetrics()
    {
        using var meterListener = new TestMeterListener(
            ApplicationTelemetry.MeterName,
            InfrastructureTelemetry.MeterName);
        var handler = new FakeHttpMessageHandler(_ =>
            CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null));
        var executor = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest("test prompt"));

        Assert.True(outcome is CapabilityExecutionFailed);

        var requests = meterListener.GetCounterValues("lunar.provider.requests");
        Assert.NotEmpty(requests);

        var durations = meterListener.GetHistogramValues("lunar.provider.request.duration");
        Assert.NotEmpty(durations);

        var failureDurations = durations.Where(r =>
            r.Tags.TryGetValue(InfrastructureTelemetry.OutcomeTag, out var outcome) &&
            outcome?.ToString() == InfrastructureTelemetry.OutcomeFailure);
        Assert.NotEmpty(failureDurations);

        // No output size metric for this failed Cloudflare Workers AI request.
        // Scope the assertion to the provider/model under test so unrelated
        // provider measurements cannot contaminate this test, and so the
        // assertion cannot pass vacuously if a broken Workers AI executor
        // ever emitted an output-size measurement on failure.
        var outputSizes = meterListener.GetHistogramValues("lunar.provider.output.size")
            .Where(r => r.Tags.TryGetValue(
                InfrastructureTelemetry.ProviderNameMetricTag,
                out var provider)
                && provider?.ToString() == InfrastructureTelemetry.ProviderCloudflareWorkersAi
                && r.Tags.TryGetValue(
                InfrastructureTelemetry.ProviderModelMetricTag,
                out var model)
                && model?.ToString() == TestModelId);
        Assert.Empty(outputSizes);
    }


    [Fact]
    public async Task RealProvider_RawBodySentinel_AbsentFromLogsAndActivitiesAndMetrics()
    {
        var sentinel = "DO_NOT_LOG_THIS_PROVIDER_BODY";
        var provider = new CaptureLoggerProvider();
        using var listener = new TestActivityListener(
            ApplicationTelemetry.ActivitySourceName,
            InfrastructureTelemetry.ActivitySourceName);
        using var meterListener = new TestMeterListener(
            ApplicationTelemetry.MeterName,
            InfrastructureTelemetry.MeterName);

        var handler = new FakeHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.TooManyRequests)
            {
                Content = new StringContent(
                    $"{{\"success\":false,\"errors\":[{{\"code\":9999,\"message\":\"{sentinel}\"}}]}}",
                    Encoding.UTF8, "application/json")
            });
        var executor = CreateExecutor(handler, provider);

        await executor.ExecuteAsync(CreateRequest("test prompt"));

        // Logs must not contain sentinel
        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n", provider.Entries
            .SelectMany(e => e.Properties.Values)
            .Select(v => v?.ToString() ?? string.Empty));
        Assert.DoesNotContain(sentinel, allMessages);
        Assert.DoesNotContain(sentinel, allProperties);

        // Activity tags must not contain sentinel
        foreach (var activity in listener.Activities)
        {
            foreach (var tag in activity.Tags)
            {
                var tagValue = tag.Value?.ToString() ?? string.Empty;
                Assert.DoesNotContain(sentinel, tagValue);
            }
        }

        // Metric tags must not contain sentinel
        foreach (var measurement in meterListener.AllMeasurements)
        {
            foreach (var tag in measurement.Tags)
            {
                var tagValue = tag.Value?.ToString() ?? string.Empty;
                Assert.DoesNotContain(sentinel, tagValue);
            }
        }
    }


    [Fact]
    public async Task RealProvider_UnexpectedException_ExceptionMessageAbsentFromActivity()
    {
        var sentinel = "DO_NOT_EXPORT_THIS_EXCEPTION_MESSAGE";
        using var listener = new TestActivityListener(
            ApplicationTelemetry.ActivitySourceName,
            InfrastructureTelemetry.ActivitySourceName);

        // Use InvalidOperationException because HttpRequestException is caught
        // by the CloudflareWorkersAiClient and converted to a failure result.
        // InvalidOperationException propagates to the executor's catch block.
        var handler = new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException(sentinel));
        var executor = CreateExecutor(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            executor.ExecuteAsync(CreateRequest("test prompt")));

        var providerActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == InfrastructureTelemetry.ProviderRequestActivityName
                 && a.Status == ActivityStatusCode.Error);
        Assert.NotNull(providerActivity);

        // exception.type tag must be present (controlled)
        var exceptionType = providerActivity.GetTagItem(InfrastructureTelemetry.ExceptionTypeTag)?.ToString();
        Assert.NotNull(exceptionType);
        Assert.Contains("InvalidOperationException", exceptionType);

        // No tag value may contain the sentinel
        foreach (var tag in providerActivity.Tags)
        {
            var tagValue = tag.Value?.ToString() ?? string.Empty;
            Assert.DoesNotContain(sentinel, tagValue);
        }
    }


    private static CloudflareWorkersAiTextToImageExecutor CreateExecutor(
        FakeHttpMessageHandler handler,
        CaptureLoggerProvider? loggerProvider = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri(TestBaseAddress),
            Timeout = Timeout.InfiniteTimeSpan
        };

        var options = new CloudflareWorkersAiOptions
        {
            BaseAddress = TestBaseAddress,
            AccountId = TestAccountId,
            ApiToken = TestApiToken,
            RequestTimeout = TimeSpan.FromSeconds(60),
            TextToImageModelId = TestModelId,
            TextToImageSteps = 4
        };

        var configuration = CloudflareWorkersAiConfiguration.From(options);
        var client = new CloudflareWorkersAiClient(httpClient, configuration);

        var logger = loggerProvider is not null
            ? loggerProvider.CreateLogger<CloudflareWorkersAiTextToImageExecutor>()
            : NullLogger<CloudflareWorkersAiTextToImageExecutor>.Instance;

        return new CloudflareWorkersAiTextToImageExecutor(client, logger);
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


    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responder;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = (req, ct) => Task.FromResult(responder(req));
        }

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _responder(request, cancellationToken);
        }
    }
}
