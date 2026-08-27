using Lunar.Api.Contracts;
using Lunar.Api.Http;
using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

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
        GenerateArtifactService generateArtifactService,
        CancellationToken cancellationToken)
    {
        if (TryValidateRequest(request, out var validationError))
        {
            return Results.Json(validationError.Response, statusCode: validationError.StatusCode);
        }

        var assetId = new AssetId(request.AssetId);
        var workflowDefinitionId = new WorkflowDefinitionId(request.WorkflowDefinitionId);
        var input = new TextPromptInput(request.Prompt);

        var result = await generateArtifactService.GenerateAsync(
            assetId,
            workflowDefinitionId,
            request.WorkflowDefinitionVersion,
            request.StepPosition,
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


    private static bool TryValidateRequest(GenerationRequest request, out HttpErrorResult error)
    {
        if (request.AssetId == Guid.Empty)
        {
            error = BadRequest("invalid_asset_id", "AssetId must be a valid non-empty UUID.");
            return true;
        }

        if (request.WorkflowDefinitionId == Guid.Empty)
        {
            error = BadRequest("invalid_workflow_definition_id", "WorkflowDefinitionId must be a valid non-empty UUID.");
            return true;
        }

        if (request.WorkflowDefinitionVersion < 1)
        {
            error = BadRequest("invalid_workflow_definition_version", "WorkflowDefinitionVersion must be >= 1.");
            return true;
        }

        if (request.StepPosition < 1)
        {
            error = BadRequest("invalid_step_position", "StepPosition must be >= 1.");
            return true;
        }

        if (string.IsNullOrWhiteSpace(request.Prompt))
        {
            error = BadRequest("invalid_prompt", "Prompt cannot be null, empty, or whitespace.");
            return true;
        }

        error = null!;
        return false;
    }


    private static HttpErrorResult BadRequest(string code, string message)
    {
        return new HttpErrorResult(
            StatusCodes.Status400BadRequest,
            new ApiErrorResponse
            {
                Code = code,
                Message = message
            });
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
