using Lunar.Core.Capabilities;

namespace Lunar.Infrastructure.Providers;

/// <summary>
/// Infrastructure composition-root implementation of
/// <see cref="ICapabilityExecutorResolver"/>. Resolves executors by
/// typed <see cref="CapabilityId"/> from a deterministic dictionary
/// built at registration time. Returns <c>null</c> for unknown
/// capabilities, allowing the Application layer to produce an expected
/// <see cref="ApplicationError"/> rather than throwing.
///
/// Duplicate capability registrations are rejected at construction
/// time so that a misconfigured composition root fails deterministically
/// rather than silently overwriting a mapping.
/// </summary>
public sealed class CapabilityExecutorResolver : ICapabilityExecutorResolver
{
    private readonly IReadOnlyDictionary<CapabilityId, ICapabilityExecutor> _executors;

    public CapabilityExecutorResolver(IReadOnlyDictionary<CapabilityId, ICapabilityExecutor> executors)
    {
        ArgumentNullException.ThrowIfNull(executors);

        _executors = executors;
    }


    public ICapabilityExecutor? Resolve(CapabilityId capabilityId)
    {
        return _executors.TryGetValue(capabilityId, out var executor) ? executor : null;
    }


    /// <summary>
    /// Builds a <see cref="CapabilityExecutorResolver"/> from an
    /// enumerable of capability-to-executor pairs, rejecting duplicate
    /// capability IDs deterministically.
    /// </summary>
    public static CapabilityExecutorResolver Create(
        IEnumerable<KeyValuePair<CapabilityId, ICapabilityExecutor>> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);

        var dictionary = new Dictionary<CapabilityId, ICapabilityExecutor>();

        foreach (var (capabilityId, executor) in mappings)
        {
            if (!dictionary.TryAdd(capabilityId, executor))
            {
                throw new InvalidOperationException(
                    $"Duplicate capability executor registration for CapabilityId {capabilityId.Value}.");
            }
        }

        return new CapabilityExecutorResolver(dictionary);
    }
}
