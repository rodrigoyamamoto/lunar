using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Api;

public sealed class DeterministicCapabilityExecutor : ICapabilityExecutor
{
    private static readonly BinaryArtifactContent DefaultContent =
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 },
            "image/jpeg");

    private readonly List<CapabilityExecutionRequest> _capturedRequests = new();

    public int CallCount { get; private set; }

    public IReadOnlyList<CapabilityExecutionRequest> CapturedRequests => _capturedRequests;

    public ArtifactContent Content { get; set; } = DefaultContent;

    public string ArtifactName { get; set; } = "test-output.jpg";

    public ArtifactType ArtifactType { get; set; } = ArtifactType.ConceptImage;

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

        var output = new CapabilityExecutionOutput(
            ArtifactName,
            ArtifactType,
            Array.Empty<ArtifactId>(),
            Content);

        return Task.FromResult<CapabilityExecutionOutcome>(
            new CapabilityExecutionSucceeded(output));
    }


    public void Reset()
    {
        CallCount = 0;
        _capturedRequests.Clear();
        Failure = null;
        Content = DefaultContent;
        ArtifactName = "test-output.jpg";
        ArtifactType = ArtifactType.ConceptImage;
    }
}
