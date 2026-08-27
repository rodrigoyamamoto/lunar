using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Lunar.Infrastructure.Providers.Cloudflare;

public sealed class CloudflareWorkersAiClient
{
    private readonly HttpClient _httpClient;
    private readonly CloudflareWorkersAiConfiguration _configuration;

    public CloudflareWorkersAiClient(
        HttpClient httpClient,
        CloudflareWorkersAiConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(configuration);

        _httpClient = httpClient;
        _configuration = configuration;
    }


    internal string TextToImageModelId => _configuration.TextToImageModelId;

    internal async Task<CloudflareImageGenerationResult> GenerateImageAsync(
        string prompt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        cancellationToken.ThrowIfCancellationRequested();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        linkedCts.CancelAfter(_configuration.RequestTimeout);

        var requestBody = new CloudflareTextToImageRequest(
            prompt,
            _configuration.TextToImageSteps);

        var requestUri = $"client/v4/accounts/{_configuration.AccountId}/ai/run/{_configuration.TextToImageModelId}";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            _configuration.ApiToken);
        httpRequest.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return FailRemoteOutcomeUnknown();
        }
        catch (HttpRequestException)
        {
            return FailRemoteOutcomeUnknown();
        }

        using (response)
        {
            try
            {
                return await CloudflareWorkersAiResponseParser.ParseAsync(
                    response,
                    linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                return ClassifyAfterHeadersReceived(response);
            }
            catch (HttpRequestException)
            {
                return ClassifyAfterHeadersReceived(response);
            }
            catch (IOException)
            {
                return ClassifyAfterHeadersReceived(response);
            }
        }
    }


    private CloudflareImageGenerationResult ClassifyAfterHeadersReceived(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return FailRemoteOutcomeUnknown();
        }

        return new CloudflareImageGenerationFailed(
            new CloudflareImageGenerationFailure(
                CloudflareWorkersAiResponseParser.ClassifyByHttpStatus(response.StatusCode),
                null));
    }


    private static CloudflareImageGenerationResult FailRemoteOutcomeUnknown() =>
        new CloudflareImageGenerationFailed(
            new CloudflareImageGenerationFailure(
                Lunar.Core.Capabilities.CapabilityExecutionFailureKind.RemoteOutcomeUnknown,
                null));


    private sealed record CloudflareTextToImageRequest(
        [property: JsonPropertyName("prompt")] string Prompt,
        [property: JsonPropertyName("steps")] int Steps);
}
