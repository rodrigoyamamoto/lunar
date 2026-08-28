using System.Net;
using System.Net.Http.Headers;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Logging;

namespace Lunar.Tests.Telemetry;

/// <summary>
/// Real-production Infrastructure privacy tests for the foreground-isolation
/// provider path. These tests execute the actual
/// <see cref="CloudflareForegroundIsolationClient"/> and
/// <see cref="CloudflareImagesForegroundIsolationExecutor"/> against a
/// controlled fake <see cref="HttpMessageHandler"/>, then prove that
/// Infrastructure telemetry (Activities, metrics, structured logs) does
/// not leak sensitive sentinels.
///
/// The previous <see cref="ForegroundIsolationPrivacyTests"/> used a fake
/// executor that bypassed the real provider path, making Infrastructure
/// telemetry assertions potentially vacuous. These tests close that gap.
/// </summary>
[Collection("Telemetry")]
public class CloudflareForegroundIsolationPrivacyTests
{
    private const string ServiceTokenSentinel = "FOREGROUND_SECRET_SENTINEL_7A9C";

    private static readonly byte[] SourceImageBytes =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0x53, 0x45, 0x43, 0x52, 0x54, 0x53, 0x52, 0x43 };

    private static readonly byte[] PngOutputBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03 };

    private const string LocalPathSentinel = "/private/lunar/data/artifacts/DO_NOT_LEAK";
    private const string ProviderPrivateErrorSentinel = "BINDING_PRIVATE_DETAIL_91F2";

    private static readonly string[] TelemetrySentinels =
    {
        ServiceTokenSentinel,
        "Authorization",
        "Bearer ",
        LocalPathSentinel,
        ProviderPrivateErrorSentinel,
    };


    [Fact]
    public async Task Success_RealExecutorAndClient_InfrastructureActivityCaptured()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_InfrastructureMetricCaptured()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var providerRequestMeasurements = capture.MeterListener
            .GetCounterValues("lunar.provider.requests");

        Assert.NotEmpty(providerRequestMeasurements);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_ProviderStructuredLogCaptured()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var providerLog = capture.LoggerProvider.Entries
            .FirstOrDefault(e => e.Message.Contains("Provider foreground isolation completed"));

        Assert.NotNull(providerLog);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_ActivityTagsNeverContainSentinels()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);

        var allTagValues = string.Join("\n",
            providerActivity!.Tags.Select(t => t.Value?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allTagValues, "Activity tags");

        // Also check byte sentinels
        var sourceHex = Convert.ToHexString(SourceImageBytes);
        var sourceBase64 = Convert.ToBase64String(SourceImageBytes);
        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        Assert.DoesNotContain(sourceHex, allTagValues);
        Assert.DoesNotContain(sourceBase64, allTagValues);
        Assert.DoesNotContain(outputHex, allTagValues);
        Assert.DoesNotContain(outputBase64, allTagValues);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_MetricTagsNeverContainSentinels()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var allTagValues = string.Join("\n",
            capture.MeterListener.AllMeasurements
                .SelectMany(m => m.Tags.Values)
                .Select(v => v?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allTagValues, "Metric tags");

        var sourceHex = Convert.ToHexString(SourceImageBytes);
        var sourceBase64 = Convert.ToBase64String(SourceImageBytes);
        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        Assert.DoesNotContain(sourceHex, allTagValues);
        Assert.DoesNotContain(sourceBase64, allTagValues);
        Assert.DoesNotContain(outputHex, allTagValues);
        Assert.DoesNotContain(outputBase64, allTagValues);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_LogsNeverContainSentinels()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values)
                .Select(v => v?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allMessages, "Log messages");
        AssertSentinelsAbsent(allProperties, "Log properties");

        var sourceHex = Convert.ToHexString(SourceImageBytes);
        var sourceBase64 = Convert.ToBase64String(SourceImageBytes);
        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        Assert.DoesNotContain(sourceHex, allMessages);
        Assert.DoesNotContain(sourceBase64, allMessages);
        Assert.DoesNotContain(outputHex, allMessages);
        Assert.DoesNotContain(outputBase64, allMessages);
    }

    [Fact]
    public async Task Success_RealExecutorAndClient_BoundedTelemetryPresent()
    {
        using var capture = await ExecuteSuccessPathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);

        // Bounded provider/operation tags should be present
        var providerNameTag = providerActivity!.Tags
            .FirstOrDefault(t => t.Key == InfrastructureTelemetry.ProviderNameTag);
        Assert.Equal(InfrastructureTelemetry.ProviderCloudflareImages, providerNameTag.Value);

        var operationTag = providerActivity.Tags
            .FirstOrDefault(t => t.Key == InfrastructureTelemetry.ProviderOperationTag);
        Assert.Equal(InfrastructureTelemetry.OperationForegroundIsolation, operationTag.Value);

        var outcomeTag = providerActivity.Tags
            .FirstOrDefault(t => t.Key == InfrastructureTelemetry.OperationOutcomeTag);
        Assert.Equal(InfrastructureTelemetry.OutcomeSuccess, outcomeTag.Value);
    }

    [Fact]
    public async Task Success_RealClient_OutboundRequestContainsAuthorizationAndRawBytes()
    {
        using var capture = await ExecuteSuccessPathAsync();

        // The fake handler captured the outbound request.
        // The service token MUST be present in the Authorization header
        // (this is the one place it is expected to exist).
        Assert.NotNull(capture.CapturedRequest);
        var authHeader = capture.CapturedRequest!.Headers.Authorization;
        Assert.NotNull(authHeader);
        Assert.Equal("Bearer", authHeader!.Scheme);
        Assert.Equal(ServiceTokenSentinel, authHeader.Parameter);

        // Content-Type must match the source image media type
        Assert.Equal("image/jpeg", capture.CapturedContentType);

        // Raw binary body must equal source bytes exactly (no Base64)
        Assert.NotNull(capture.CapturedRequestBytes);
        Assert.Equal(SourceImageBytes, capture.CapturedRequestBytes);
    }


    [Fact]
    public async Task Failure_RealExecutorAndClient_InfrastructureActivityCaptured()
    {
        using var capture = await ExecuteFailurePathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);
    }

    [Fact]
    public async Task Failure_RealExecutorAndClient_InfrastructureMetricCaptured()
    {
        using var capture = await ExecuteFailurePathAsync();

        var durationMeasurements = capture.MeterListener
            .GetHistogramValues("lunar.provider.request.duration");

        Assert.NotEmpty(durationMeasurements);
    }

    [Fact]
    public async Task Failure_RealExecutorAndClient_FailureOutcomeCaptured()
    {
        using var capture = await ExecuteFailurePathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);

        var outcomeTag = providerActivity!.Tags
            .FirstOrDefault(t => t.Key == InfrastructureTelemetry.OperationOutcomeTag);
        Assert.Equal(InfrastructureTelemetry.OutcomeFailure, outcomeTag.Value);

        var failureKindTag = providerActivity.Tags
            .FirstOrDefault(t => t.Key == InfrastructureTelemetry.FailureKindTag);
        Assert.NotNull(failureKindTag.Value);
    }

    [Fact]
    public async Task Failure_RealExecutorAndClient_ActivityTagsNeverContainSentinels()
    {
        using var capture = await ExecuteFailurePathAsync();

        var providerActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == InfrastructureTelemetry.ProviderRequestActivityName);

        Assert.NotNull(providerActivity);

        var allTagValues = string.Join("\n",
            providerActivity!.Tags.Select(t => t.Value?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allTagValues, "Activity tags (failure)");

        // The raw provider body sentinel must not appear in telemetry
        Assert.DoesNotContain(ProviderPrivateErrorSentinel, allTagValues);
    }

    [Fact]
    public async Task Failure_RealExecutorAndClient_MetricTagsNeverContainSentinels()
    {
        using var capture = await ExecuteFailurePathAsync();

        var allTagValues = string.Join("\n",
            capture.MeterListener.AllMeasurements
                .SelectMany(m => m.Tags.Values)
                .Select(v => v?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allTagValues, "Metric tags (failure)");
        Assert.DoesNotContain(ProviderPrivateErrorSentinel, allTagValues);
    }

    [Fact]
    public async Task Failure_RealExecutorAndClient_LogsNeverContainProviderBody()
    {
        using var capture = await ExecuteFailurePathAsync();

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values)
                .Select(v => v?.ToString() ?? string.Empty));

        AssertSentinelsAbsent(allMessages, "Log messages (failure)");
        AssertSentinelsAbsent(allProperties, "Log properties (failure)");

        // The raw provider response body must not be logged
        Assert.DoesNotContain(ProviderPrivateErrorSentinel, allMessages);
        Assert.DoesNotContain(ProviderPrivateErrorSentinel, allProperties);
    }


    private static void AssertSentinelsAbsent(string content, string surface)
    {
        foreach (var sentinel in TelemetrySentinels)
        {
            Assert.DoesNotContain(sentinel, content);
        }
    }


    private static async Task<ProviderCapture> ExecuteSuccessPathAsync()
    {
        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.OK,
            PngOutputBytes,
            "image/png");

        return await ExecuteProviderAsync(handler);
    }

    private static async Task<ProviderCapture> ExecuteFailurePathAsync()
    {
        var failureBody = System.Text.Encoding.UTF8.GetBytes(
            $"{{\"error\":{{\"detail\":\"{ProviderPrivateErrorSentinel}\"}}}}");

        var handler = new FakeHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            failureBody,
            "application/json");

        return await ExecuteProviderAsync(handler);
    }

    private static async Task<ProviderCapture> ExecuteProviderAsync(FakeHttpMessageHandler handler)
    {
        var loggerProvider = new CaptureLoggerProvider();
        var activityListener = new TestActivityListener(
            InfrastructureTelemetry.ActivitySource.Name);
        var meterListener = new TestMeterListener(
            InfrastructureTelemetry.Meter.Name);

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://test-worker.example.com/"),
            Timeout = Timeout.InfiniteTimeSpan,
        };

        var configuration = CloudflareForegroundIsolationConfiguration.From(
            new CloudflareForegroundIsolationOptions
            {
                Endpoint = "https://test-worker.example.com/",
                ServiceToken = ServiceTokenSentinel,
                RequestTimeout = TimeSpan.FromMinutes(2),
            });

        var client = new CloudflareForegroundIsolationClient(httpClient, configuration);

        var executor = new CloudflareImagesForegroundIsolationExecutor(
            client,
            loggerProvider.CreateLogger<CloudflareImagesForegroundIsolationExecutor>());

        var input = new ImageArtifactInput(
            new BinaryArtifactContent(SourceImageBytes, "image/jpeg"));

        var request = new CapabilityExecutionRequest(
            CapabilityId.New(),
            AssetId.New(),
            WorkflowExecutionId.New(),
            WorkflowDefinitionId.New(),
            1,
            1,
            input);

        await executor.ExecuteAsync(request);

        return new ProviderCapture(
            loggerProvider,
            activityListener,
            meterListener,
            handler.CapturedRequest,
            handler.CapturedRequestBytes,
            handler.CapturedContentType,
            httpClient);
    }


    private sealed class ProviderCapture : IDisposable
    {
        public CaptureLoggerProvider LoggerProvider { get; }
        public TestActivityListener ActivityListener { get; }
        public TestMeterListener MeterListener { get; }
        public HttpRequestMessage? CapturedRequest { get; }
        public byte[]? CapturedRequestBytes { get; }
        public string? CapturedContentType { get; }
        private readonly HttpClient _httpClient;

        public ProviderCapture(
            CaptureLoggerProvider loggerProvider,
            TestActivityListener activityListener,
            TestMeterListener meterListener,
            HttpRequestMessage? capturedRequest,
            byte[]? capturedRequestBytes,
            string? capturedContentType,
            HttpClient httpClient)
        {
            LoggerProvider = loggerProvider;
            ActivityListener = activityListener;
            MeterListener = meterListener;
            CapturedRequest = capturedRequest;
            CapturedRequestBytes = capturedRequestBytes;
            CapturedContentType = capturedContentType;
            _httpClient = httpClient;
        }

        public void Dispose()
        {
            MeterListener.Dispose();
            ActivityListener.Dispose();
            LoggerProvider.Dispose();
            _httpClient.Dispose();
            CapturedRequest?.Dispose();
        }
    }


    /// <summary>
    /// Fake HTTP handler that captures the outbound request and returns
    /// a controlled response. Does not call real Cloudflare.
    /// </summary>
    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly byte[] _body;
        private readonly string _contentType;
        private HttpRequestMessage? _capturedRequest;
        private byte[]? _capturedRequestBytes;
        private string? _capturedContentType;

        public HttpRequestMessage? CapturedRequest => _capturedRequest;
        public byte[]? CapturedRequestBytes => _capturedRequestBytes;
        public string? CapturedContentType => _capturedContentType;

        public FakeHttpMessageHandler(HttpStatusCode status, byte[] body, string contentType)
        {
            _status = status;
            _body = body;
            _contentType = contentType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Capture request bytes before the content is disposed
            if (request.Content is not null)
            {
                _capturedRequestBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
                _capturedContentType = request.Content.Headers.ContentType?.MediaType;
            }

            // Capture headers (these survive disposal)
            _capturedRequest = request;

            var response = new HttpResponseMessage(_status)
            {
                Content = new ByteArrayContent(_body),
            };

            response.Content.Headers.ContentType =
                new MediaTypeHeaderValue(_contentType);

            return response;
        }
    }
}
