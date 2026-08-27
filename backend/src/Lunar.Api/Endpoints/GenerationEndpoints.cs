using Lunar.Api.Contracts;
using Lunar.Api.Http;
using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;

namespace Lunar.Api.Endpoints;

public static class GenerationEndpoints
{
    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/generations");

        group.MapPost("/", CreateGenerationAsync);

        return app;
    }


    private static async Task<IResult> CreateGenerationAsync(
        GenerationRequest request,
        GenerateDefaultArtifactService generateDefaultArtifactService,
        CancellationToken cancellationToken)
    {
        if (request.AssetId == Guid.Empty)
        {
            return BadRequest("invalid_asset_id", "AssetId must be a valid non-empty UUID.");
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            return BadRequest("invalid_prompt", "Prompt cannot be null, empty, or whitespace.");
        }

        var assetId = new AssetId(request.AssetId);
        var input = new TextPromptInput(request.Prompt);

        var result = await generateDefaultArtifactService.GenerateAsync(
            assetId,
            input,
            cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        var generated = result.Value!;
        var produced = generated.ProducedArtifact;
        var artifact = produced.Artifact;

        if (produced.Content is not BinaryArtifactContent binaryContent)
        {
            throw new InvalidOperationException(
                "The first generation API requires binary artifact content.");
        }

        var response = new GenerationResponse
        {
            WorkflowExecutionId = generated.WorkflowExecutionId.Value,
            ArtifactId = artifact.Id.Value,
            AssetId = artifact.AssetId.Value,
            ArtifactName = artifact.Name,
            ArtifactType = artifact.Type.ToString(),
            MediaType = binaryContent.MediaType,
            ContentUrl = $"/api/artifacts/{artifact.Id.Value}/content"
        };

        return Results.Created(response.ContentUrl, response);
    }


    private static IResult BadRequest(string code, string message)
    {
        return Results.Json(
            new ApiErrorResponse
            {
                Code = code,
                Message = message
            },
            statusCode: StatusCodes.Status400BadRequest);
    }


    private static IResult MapError(ApplicationError error)
    {
        var result = ApplicationErrorHttpMapping.Map(error);

        if (result.RetryAfterSeconds is { } retryAfter)
        {
            return new RetryAfterJsonResult(result.Response, result.StatusCode, retryAfter);
        }

        return Results.Json(result.Response, statusCode: result.StatusCode);
    }
}
