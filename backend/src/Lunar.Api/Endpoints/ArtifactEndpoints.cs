using Lunar.Api.Contracts;
using Lunar.Api.Http;
using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Assets;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;

namespace Lunar.Api.Endpoints;

public static class ArtifactEndpoints
{
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artifacts");

        group.MapGet("/{artifactId:guid}/content", GetArtifactContentAsync);
        group.MapPost("/{artifactId:guid}/remove-background", RemoveBackgroundAsync);

        return app;
    }


    private static async Task<IResult> GetArtifactContentAsync(
        Guid artifactId,
        GetArtifactContentService getArtifactContentService,
        CancellationToken cancellationToken)
    {
        if (artifactId == Guid.Empty)
        {
            return Results.Json(
                new Contracts.ApiErrorResponse
                {
                    Code = "invalid_artifact_id",
                    Message = "ArtifactId must be a valid non-empty UUID."
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        var result = await getArtifactContentService.GetAsync(
            new ArtifactId(artifactId),
            cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        var produced = result.Value!;

        if (produced.Content is not BinaryArtifactContent binaryContent)
        {
            throw new InvalidOperationException(
                "The first generation API requires binary artifact content.");
        }

        return Results.File(
            binaryContent.Data.ToArray(),
            contentType: binaryContent.MediaType);
    }


    private static async Task<IResult> RemoveBackgroundAsync(
        Guid artifactId,
        RemoveArtifactBackgroundService removeArtifactBackgroundService,
        CancellationToken cancellationToken)
    {
        if (artifactId == Guid.Empty)
        {
            return Results.Json(
                new Contracts.ApiErrorResponse
                {
                    Code = "invalid_artifact_id",
                    Message = "ArtifactId must be a valid non-empty UUID."
                },
                statusCode: StatusCodes.Status400BadRequest);
        }

        Result<GeneratedArtifact> result;

        try
        {
            result = await removeArtifactBackgroundService.RemoveBackgroundAsync(
                new ArtifactId(artifactId),
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

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
                "Background removal requires binary artifact content.");
        }

        var response = new ArtifactTransformationResponse
        {
            WorkflowExecutionId = generated.WorkflowExecutionId.Value,
            ArtifactId = artifact.Id.Value,
            AssetId = artifact.AssetId.Value,
            ArtifactName = artifact.Name,
            ArtifactType = artifact.Type.ToString(),
            MediaType = binaryContent.MediaType,
            ContentUrl = $"/api/artifacts/{artifact.Id.Value}/content",
            SourceArtifactIds = artifact.SourceArtifactIds
                .Select(id => id.Value)
                .ToList()
        };

        return Results.Created(response.ContentUrl, response);
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
