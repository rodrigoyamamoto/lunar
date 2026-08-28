using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Api;

public sealed class DeterministicCapabilityExecutor : ICapabilityExecutor
{
    private static readonly BinaryArtifactContent DefaultContent =
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 },
            "image/jpeg");

    private static readonly BinaryArtifactContent TransformContent =
        new(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 },
            "image/png");

    private readonly List<CapabilityExecutionRequest> _capturedRequests = new();

    public int CallCount { get; private set; }

    public IReadOnlyList<CapabilityExecutionRequest> CapturedRequests => _capturedRequests;

    public ArtifactContent Content { get; set; } = DefaultContent;

    public CapabilityExecutionFailure? Failure { get; set; }


    public Task<CapabilityExecutionOutcome> ExecuteAsync(
        CapabilityExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        CallCount++;
        _capturedRequests.Add(request);

        if (Failure is { } failure)
        {
            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionFailed(failure));
        }

        if (request.Input is ImageArtifactInput)
        {
            var output = new CapabilityExecutionOutput(TransformContent);

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }

        var defaultOutput = new CapabilityExecutionOutput(Content);

        return Task.FromResult<CapabilityExecutionOutcome>(
            new CapabilityExecutionSucceeded(defaultOutput));
    }


    public void Reset()
    {
        CallCount = 0;
        _capturedRequests.Clear();
        Failure = null;
        Content = DefaultContent;
    }
}
