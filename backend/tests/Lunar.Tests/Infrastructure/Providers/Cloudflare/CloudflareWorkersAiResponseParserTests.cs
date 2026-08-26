using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Lunar.Core.Capabilities;
using Lunar.Infrastructure.Providers.Cloudflare;

namespace Lunar.Tests.Infrastructure.Providers.Cloudflare;

public class CloudflareWorkersAiResponseParserTests
{
    private static readonly byte[] JpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10 };


    // ---- Token propagation regression tests ----

    [Fact]
    public async Task ParseAsync_PreCancelledTokenWithValidSuccessEnvelope_ShouldThrowOperationCanceledException()
    {
        // General: a token already cancelled before ParseAsync starts
        // propagates. This is not specific to payload-validation; it may
        // also cancel ReadFromJsonAsync. It confirms the parser does not
        // swallow OperationCanceledException.
        var response = CreateSuccessResponse(JpegBytes);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CloudflareWorkersAiResponseParser.ParseAsync(response, cts.Token));
    }


    [Fact]
    public async Task ParseAsync_TokenCancelledAfterBodyDelivered_ShouldThrowBeforeBase64Decode()
    {
        // Deterministic proof that the real token survives body
        // deserialization and reaches ValidateSuccessPayload's pre-decode
        // checkpoint. The custom stream delivers the full valid JSON, then
        // cancels the token, then returns 0 on the next read. If the
        // parser were to pass default to ValidateSuccessPayload, this test
        // would return CloudflareImageGenerationSucceeded instead of
        // throwing OperationCanceledException.
        var cts = new CancellationTokenSource();
        var response = CreateSuccessResponse(JpegBytes, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CloudflareWorkersAiResponseParser.ParseAsync(response, cts.Token));
    }


    [Fact]
    public async Task ParseAsync_TokenCancelledAfterBodyDelivered_ShouldNotReturnSucceeded()
    {
        // Same arrangement as above, but explicitly asserts that no
        // successful result is returned. If the parser uses default, this
        // will return success and the assertion message explains the defect.
        var cts = new CancellationTokenSource();
        var response = CreateSuccessResponse(JpegBytes, cts);

        try
        {
            var result = await CloudflareWorkersAiResponseParser.ParseAsync(response, cts.Token);
            Assert.Fail(
                "Parser must not return a succeeded result when the token is cancelled " +
                "after the response body has been fully delivered. " +
                $"Got: {result.GetType().Name}. This proves the real token did not reach " +
                "the payload validation cancellation checkpoint.");
        }
        catch (OperationCanceledException)
        {
            // Expected — the real token reached ThrowIfCancellationRequested
            // in ValidateSuccessPayload before Base64 decode.
        }
    }


    [Fact]
    public async Task ParseAsync_ValidTokenWithValidSuccessEnvelope_ShouldReturnSucceeded()
    {
        var response = CreateSuccessResponse(JpegBytes);

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var succeeded = Assert.IsType<CloudflareImageGenerationSucceeded>(result);
        Assert.Equal(JpegBytes, succeeded.ImageBytes);
    }


    [Fact]
    public async Task ParseAsync_CancelledTokenWithNonSuccessResponse_ShouldPropagateCancellation()
    {
        // Even for non-success responses, ReadFromJsonAsync observes the
        // cancellation token and throws. The parser correctly lets this
        // propagate to the Client, which owns caller-vs-timeout discrimination.
        var response = CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CloudflareWorkersAiResponseParser.ParseAsync(response, cts.Token));
    }


    // ---- HTTP 2xx + success:false tests (parser-level) ----

    [Fact]
    public async Task ParseAsync_Http200SuccessFalseWithCode3036_ShouldMapToQuotaExhausted()
    {
        var response = CreateEnvelopeResponse(HttpStatusCode.OK, false, 3036);

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, failed.Failure.Kind);
    }


    [Fact]
    public async Task ParseAsync_Http200SuccessFalseWithNoErrors_ShouldMapToInvalidResponse()
    {
        var response = CreateEnvelopeResponse(HttpStatusCode.OK, false, null);

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    // ---- Invalid response tests (parser-level) ----

    [Fact]
    public async Task ParseAsync_MalformedJson_ShouldMapToInvalidResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json", Encoding.UTF8, "application/json")
        };

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    [Fact]
    public async Task ParseAsync_InvalidBase64_ShouldMapToInvalidResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"success\":true,\"result\":{\"image\":\"not!!!valid!!!base64\"}}",
                Encoding.UTF8, "application/json")
        };

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    [Fact]
    public async Task ParseAsync_NonJpegBytes_ShouldMapToInvalidResponse()
    {
        var nonJpeg = new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };
        var response = CreateSuccessResponse(nonJpeg);

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    [Fact]
    public async Task ParseAsync_MissingSuccessField_ShouldMapToInvalidResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"result\":{\"image\":\"" + Convert.ToBase64String(JpegBytes) + "\"}}",
                Encoding.UTF8, "application/json")
        };

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    [Fact]
    public async Task ParseAsync_SuccessTrueWithMissingResult_ShouldMapToInvalidResponse()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"success\":true}", Encoding.UTF8, "application/json")
        };

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, failed.Failure.Kind);
    }


    // ---- Retry-After tests (parser-level) ----

    [Fact]
    public async Task ParseAsync_RetryAfterDeltaSeconds_ShouldBePreserved()
    {
        var response = CreateEnvelopeResponse(HttpStatusCode.TooManyRequests, false, null);
        response.Headers.Add("Retry-After", "30");

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(TimeSpan.FromSeconds(30), failed.Failure.RetryAfter);
    }


    // ---- Known-code-not-first test (parser-level) ----

    [Fact]
    public async Task ParseAsync_KnownCodeNotFirstInErrors_ShouldStillMapCorrectly()
    {
        var errors = string.Join(",", new[] { 9999, 3036 }.Select(c =>
            $"{{\"code\":{c},\"message\":\"error\"}}"));
        var json = $"{{\"success\":false,\"errors\":[{errors}]}}";
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var result = await CloudflareWorkersAiResponseParser.ParseAsync(
            response,
            CancellationToken.None);

        var failed = Assert.IsType<CloudflareImageGenerationFailed>(result);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, failed.Failure.Kind);
    }


    // ---- Helpers ----

    private static HttpResponseMessage CreateSuccessResponse(
        byte[] imageBytes,
        CancellationTokenSource? cts = null)
    {
        var base64 = Convert.ToBase64String(imageBytes);
        var json = $"{{\"success\":true,\"result\":{{\"image\":\"{base64}\"}}}}";

        HttpContent content = cts is null
            ? new StringContent(json, Encoding.UTF8, "application/json")
            : new CancellingAfterDeliveryHttpContent(json, cts);

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = content
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


    /// <summary>
    /// Test-only HttpContent that delivers a complete JSON payload on the
    /// first read, then cancels the supplied CancellationTokenSource, then
    /// returns 0 on subsequent reads without observing the now-cancelled
    /// token. This lets tests prove the parser reaches payload validation
    /// with a token that is cancelled only after the body has been fully
    /// delivered.
    /// </summary>
    private sealed class CancellingAfterDeliveryHttpContent : HttpContent
    {
        private readonly byte[] _bytes;
        private readonly CancellationTokenSource _cts;

        public CancellingAfterDeliveryHttpContent(string json, CancellationTokenSource cts)
        {
            _bytes = Encoding.UTF8.GetBytes(json);
            _cts = cts;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }


        protected override Task<Stream> CreateContentReadStreamAsync()
        {
            return Task.FromResult<Stream>(new CancellingAfterDeliveryStream(_bytes, _cts));
        }


        protected override Task SerializeToStreamAsync(
            Stream stream,
            TransportContext? context)
        {
            stream.Write(_bytes, 0, _bytes.Length);
            return Task.CompletedTask;
        }


        protected override bool TryComputeLength(out long length)
        {
            length = -1;
            return false;
        }


        private sealed class CancellingAfterDeliveryStream : Stream
        {
            private readonly byte[] _bytes;
            private readonly CancellationTokenSource _cts;
            private int _position;
            private bool _delivered;

            public CancellingAfterDeliveryStream(byte[] bytes, CancellationTokenSource cts)
            {
                _bytes = bytes;
                _cts = cts;
            }


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
                if (!_delivered)
                {
                    _delivered = true;

                    var available = _bytes.Length - _position;
                    var toCopy = Math.Min(available, count);

                    Buffer.BlockCopy(_bytes, _position, buffer, offset, toCopy);
                    _position += toCopy;

                    // Cancel the token after delivering the complete payload.
                    // If multiple reads are needed to consume the buffer, this
                    // only cancels after the first read; the test JSON is small
                    // enough that one read is sufficient for the default buffer.
                    if (_position >= _bytes.Length)
                    {
                        _cts.Cancel();
                    }

                    return Task.FromResult(toCopy);
                }

                // End of stream. Do not throw even though the token is now
                // cancelled — we want ReadFromJsonAsync to finish successfully.
                return Task.FromResult(0);
            }


            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException("Use ReadAsync for this test double.");
            }


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
