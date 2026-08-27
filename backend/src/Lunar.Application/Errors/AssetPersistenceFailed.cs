using Lunar.Core.Assets;

namespace Lunar.Application.Errors;

public sealed record AssetPersistenceFailed(AssetId AssetId) : ApplicationError(
    $"Asset persistence failed for {AssetId}.");
