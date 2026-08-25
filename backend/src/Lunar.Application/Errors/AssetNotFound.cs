using Lunar.Core.Assets;

namespace Lunar.Application.Errors;

public sealed record AssetNotFound(AssetId AssetId) : ApplicationError(
    $"Asset not found for {AssetId}.");
