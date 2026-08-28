using Lunar.Core.Capabilities;

namespace Lunar.Application.Errors;

public sealed record CapabilityExecutorNotFound(CapabilityId CapabilityId) : ApplicationError(
    $"No capability executor is configured for {CapabilityId}.");
