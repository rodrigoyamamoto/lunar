using Lunar.Api.Http;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Core.Artifacts;

namespace Lunar.Api.Endpoints;

public static class ArtifactEndpoints
{
    public static IEndpointRouteBuilder MapArtifactEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/artifacts");

        group.MapGet("/{artifactId:guid}/content", GetArtifactContentAsync);

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
