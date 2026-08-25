namespace Lunar.Core.Capabilities;

public interface ICapabilityExecutor
{
    Task<CapabilityExecutionOutput> ExecuteAsync(
        CapabilityExecutionRequest request,
        CancellationToken cancellationToken = default);
}
