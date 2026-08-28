using System.Net;
using System.Net.Http.Headers;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Infrastructure.Providers.Cloudflare;

public class CloudflareImagesForegroundIsolationExecutorTests
{
    private static readonly byte[] JpegInput = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };
    private static readonly byte[] PngOutput = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };

    private static readonly Uri Endpoint = new("https://worker.example.com/");
    private const string ServiceToken = "test-service-token";


    [Fact]
    public async Task ExecuteAsync_ValidJpegInput_ReturnsPngSuccess()
    {
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            PngOutput,
            "image/png");
        var (executor, _) = CreateExecutor(handler);

        var request = CreateRequest(JpegInput, "image/jpeg");

        var outcome = await executor.ExecuteAsync(request);

        var succeeded = Assert.IsType<CapabilityExecutionSucceeded>(outcome);
        var content = Assert.IsType<BinaryArtifactContent>(succeeded.Output.Content);
        Assert.Equal("image/png", content.MediaType);
        Assert.Equal(PngOutput, content.Data.ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_SendsRawBytesWithSourceMediaType()
    {
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            PngOutput,
            "image/png");
        var (executor, _) = CreateExecutor(handler);

        var request = CreateRequest(JpegInput, "image/jpeg");

        await executor.ExecuteAsync(request);

        Assert.Equal("image/jpeg", handler.CapturedRequestMediaType);
        Assert.Equal(JpegInput, handler.CapturedRequestBytes);
    }

    [Fact]
    public async Task ExecuteAsync_AppliesServiceAuthorization()
    {
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            PngOutput,
            "image/png");
        var (executor, _) = CreateExecutor(handler);

        var request = CreateRequest(JpegInput, "image/jpeg");

        await executor.ExecuteAsync(request);

        Assert.Equal("Bearer", handler.CapturedAuthScheme);
        Assert.Equal(ServiceToken, handler.CapturedAuthToken);
    }

    [Fact]
    public async Task ExecuteAsync_OutputCarriesOnlyContent()
    {
        // After the metadata refactor, CapabilityExecutionOutput carries only
        // content. Name, type, and lineage are owned by the Application
        // workflow execution context. The executor only transforms bytes.
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            PngOutput,
            "image/png");
        var (executor, _) = CreateExecutor(handler);

        var request = CreateRequest(JpegInput, "image/jpeg");

        var outcome = await executor.ExecuteAsync(request);

        var succeeded = Assert.IsType<CapabilityExecutionSucceeded>(outcome);
        var properties = succeeded.Output.GetType().GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "ArtifactName");
        Assert.DoesNotContain(properties, p => p.Name == "ArtifactType");
        Assert.DoesNotContain(properties, p => p.Name == "SourceArtifactIds");
    }

    [Fact]
    public async Task ExecuteAsync_401_ReturnsAuthenticationFailed()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Unauthorized);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.AuthenticationFailed, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_403_ReturnsAccessDenied()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.Forbidden);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.AccessDenied, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_429_ReturnsRateLimited()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.TooManyRequests);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_500_ReturnsTemporarilyUnavailable()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.InternalServerError);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_408_ReturnsTimedOut()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.RequestTimeout);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TimedOut, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_Other4xx_ReturnsRejected()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.BadRequest);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.Rejected, failed.Failure.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ExecuteAsync_5xx_ReturnsTemporarilyUnavailable(HttpStatusCode status)
    {
        var handler = new FakeHttpHandler(status);
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyBody_ReturnsInvalidResponse()
    {
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            Array.Empty<byte>(),
            "image/png");
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_WrongMediaType_ReturnsInvalidResponse()
    {
        var handler = new FakeHttpHandler(
            HttpStatusCode.OK,
            JpegInput,
            "image/jpeg");
        var (executor, _) = CreateExecutor(handler);

        var outcome = await executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"));

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }

    [Fact]
    public async Task ExecuteAsync_NonImageArtifactInput_Throws()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, PngOutput, "image/png");
        var (executor, _) = CreateExecutor(handler);

        var request = new CapabilityExecutionRequest(
            CapabilityId.New(),
            AssetId.New(),
            Lunar.Core.Workflows.WorkflowExecutionId.New(),
            Lunar.Core.Workflows.WorkflowDefinitionId.New(),
            1, 1,
            new TextPromptInput("test"));

        await Assert.ThrowsAsync<ArgumentException>(() => executor.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_InputExceeding20MB_ReturnsRejected()
    {
        var handler = new FakeHttpHandler(HttpStatusCode.OK, PngOutput, "image/png");
        var (executor, _) = CreateExecutor(handler);

        var oversized = new byte[20 * 1024 * 1024 + 1];
        var request = CreateRequest(oversized, "image/jpeg");

        var outcome = await executor.ExecuteAsync(request);

        var failed = Assert.IsType<CapabilityExecutionFailed>(outcome);
        Assert.Equal(CapabilityExecutionFailureKind.Rejected, failed.Failure.Kind);
    }


    [Fact]
    public async Task ExecuteAsync_Cancellation_Propagates()
    {
        var handler = new SlowHttpHandler();
        var (executor, _) = CreateExecutor(handler);

        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(CreateRequest(JpegInput, "image/jpeg"), cts.Token));
    }


    private static CapabilityExecutionRequest CreateRequest(
        byte[] imageBytes,
        string mediaType)
    {
        var content = new BinaryArtifactContent(imageBytes, mediaType);
        var input = new ImageArtifactInput(content);

        return new CapabilityExecutionRequest(
            CapabilityId.New(),
            AssetId.New(),
            Lunar.Core.Workflows.WorkflowExecutionId.New(),
            Lunar.Core.Workflows.WorkflowDefinitionId.New(),
            1, 1,
            input);
    }

    private static (CloudflareImagesForegroundIsolationExecutor, FakeHttpHandler) CreateExecutor(
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = Endpoint
        };

        var options = new CloudflareForegroundIsolationOptions
        {
            Endpoint = Endpoint.ToString(),
            ServiceToken = ServiceToken,
            RequestTimeout = TimeSpan.FromSeconds(30)
        };
        var config = CloudflareForegroundIsolationConfiguration.From(options);

        var client = new CloudflareForegroundIsolationClient(httpClient, config);
        var executor = new CloudflareImagesForegroundIsolationExecutor(
            client,
            NullLogger<CloudflareImagesForegroundIsolationExecutor>.Instance);

        return (executor, handler is FakeHttpHandler fake ? fake : null!);
    }


    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly byte[] _responseBody;
        private readonly string _responseMediaType;

        public string? CapturedAuthScheme { get; private set; }
        public string? CapturedAuthToken { get; private set; }
        public string? CapturedRequestMediaType { get; private set; }
        public byte[]? CapturedRequestBytes { get; private set; }

        public FakeHttpHandler(
            HttpStatusCode statusCode,
            byte[]? responseBody = null,
            string? responseMediaType = null)
        {
            _statusCode = statusCode;
            _responseBody = responseBody ?? Array.Empty<byte>();
            _responseMediaType = responseMediaType ?? "application/octet-stream";
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.Headers.Authorization is { } auth)
            {
                CapturedAuthScheme = auth.Scheme;
                CapturedAuthToken = auth.Parameter;
            }

            if (request.Content is ByteArrayContent byteArray)
            {
                CapturedRequestMediaType = request.Content.Headers.ContentType?.MediaType;
                CapturedRequestBytes = await byteArray.ReadAsByteArrayAsync(cancellationToken);
            }

            var response = new HttpResponseMessage(_statusCode);

            if (_statusCode == HttpStatusCode.OK && _responseBody.Length > 0)
            {
                response.Content = new ByteArrayContent(_responseBody);
                response.Content.Headers.ContentType = new MediaTypeHeaderValue(_responseMediaType);
            }
            else if (_statusCode != HttpStatusCode.OK)
            {
                response.Content = new StringContent("{}");
                response.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }

            return response;
        }
    }

    private sealed class SlowHttpHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
