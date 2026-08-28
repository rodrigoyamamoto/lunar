using Lunar.Core.Capabilities;

namespace Lunar.Tests;

/// <summary>
/// Test helper that wraps a single <see cref="ICapabilityExecutor"/> as
/// an <see cref="ICapabilityExecutorResolver"/> that resolves the same
/// executor for any <see cref="CapabilityId"/>. This is appropriate for
/// unit tests that exercise only one capability and construct their
/// own workflow definitions with arbitrary capability IDs.
/// </summary>
public sealed class SingleCapabilityExecutorResolver : ICapabilityExecutorResolver
{
    private readonly ICapabilityExecutor _executor;

    public SingleCapabilityExecutorResolver(ICapabilityExecutor executor)
    {
        ArgumentNullException.ThrowIfNull(executor);
        _executor = executor;
    }

    public ICapabilityExecutor? Resolve(CapabilityId capabilityId) => _executor;
}
