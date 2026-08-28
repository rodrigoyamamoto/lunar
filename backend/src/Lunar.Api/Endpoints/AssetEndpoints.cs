using Lunar.Api.Contracts;
using Lunar.Api.Http;
using Lunar.Application.Assets;
using Lunar.Application.Errors;
using Lunar.Core.Assets;
using Lunar.Core.Artifacts;

namespace Lunar.Api.Endpoints;

public static class AssetEndpoints
{
    private static readonly HashSet<string> ValidAssetTypeNames =
        new(Enum.GetNames<AssetType>(), StringComparer.Ordinal);

    public static IEndpointRouteBuilder MapAssetEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/assets");

        group.MapPost("/", CreateAssetAsync);
        group.MapGet("/{assetId}/artifacts", ListAssetArtifactsAsync);

        return app;
    }


    private static async Task<IResult> CreateAssetAsync(
        CreateAssetRequest request,
        CreateAssetService createAssetService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest("invalid_name", "Name cannot be null, empty, or whitespace.");
        }

        if (!TryParseAssetType(request.AssetType, out var assetType))
        {
            return BadRequest("invalid_asset_type", "AssetType must be one of: Character, Weapon, Environment, Prop.");
        }

        var result = await createAssetService.CreateAsync(
            request.Name,
            assetType,
            cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        var asset = result.Value!;

        var response = new CreateAssetResponse
        {
            AssetId = asset.Id.Value,
            Name = asset.Name,
            AssetType = asset.Type.ToString()
        };

        return Results.Created($"/api/assets/{asset.Id.Value}", response);
    }


    private static async Task<IResult> ListAssetArtifactsAsync(
        Guid assetId,
        ListAssetArtifactsService listAssetArtifactsService,
        CancellationToken cancellationToken)
    {
        if (assetId == Guid.Empty)
        {
            return BadRequest("invalid_asset_id", "AssetId must be a valid non-empty UUID.");
        }

        var result = await listAssetArtifactsService.ListAsync(
            new AssetId(assetId),
            cancellationToken);

        if (result.IsFailure)
        {
            return MapError(result.Error!);
        }

        var artifacts = result.Value!;

        var response = artifacts
            .Select(item => new ArtifactSummaryResponse
            {
                ArtifactId = item.Artifact.Id.Value,
                AssetId = item.Artifact.AssetId.Value,
                ArtifactName = item.Artifact.Name,
                ArtifactType = item.Artifact.Type.ToString(),
                CreatedAt = item.Artifact.CreatedAt,
                ContentUrl = $"/api/artifacts/{item.Artifact.Id.Value}/content",
                GenerationInput = item.GenerationInput is { } input
                    ? new GenerationInputResponse
                    {
                        WorkflowExecutionId = input.WorkflowExecutionId.Value,
                        Prompt = input.Prompt.Prompt
                    }
                    : null
            })
            .ToList();

        return Results.Ok(response);
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


    private static bool TryParseAssetType(string? value, out AssetType assetType)
    {
        assetType = default;

        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        if (!ValidAssetTypeNames.Contains(value))
        {
            return false;
        }

        assetType = Enum.Parse<AssetType>(value);
        return true;
    }
}
