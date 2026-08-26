namespace Lunar.Core.Capabilities;

public interface ICapabilityExecutor
{
    Task<CapabilityExecutionOutcome> ExecuteAsync(
        CapabilityExecutionRequest request,
        CancellationToken cancellationToken = default);
}
