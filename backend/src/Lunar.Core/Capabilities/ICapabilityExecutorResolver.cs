namespace Lunar.Core.Capabilities;

/// <summary>
/// Resolves a <see cref="ICapabilityExecutor"/> for a given
/// <see cref="CapabilityId"/>. This is the provider-independent routing
/// boundary between workflow step capabilities and their configured
/// Infrastructure executors. The composition root registers executors
/// deterministically; Application code never performs service-locator
/// lookups.
/// </summary>
public interface ICapabilityExecutorResolver
{
    /// <summary>
    /// Attempts to resolve the executor for the specified capability.
    /// Returns <c>null</c> when no executor is configured for the
    /// capability, allowing the caller to produce an expected
    /// <see cref="ApplicationError"/> rather than throwing.
    /// </summary>
    ICapabilityExecutor? Resolve(CapabilityId capabilityId);
}
