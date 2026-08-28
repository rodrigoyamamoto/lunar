using Lunar.Core.Artifacts;

namespace Lunar.Core.Capabilities;

/// <summary>
/// Typed capability input for operations that transform an existing
/// image. Carries the resolved binary image content so that provider
/// executors receive the actual image bytes without reaching into
/// Lunar repositories.
///
/// Direct Artifact lineage is owned by the Application/workflow
/// execution context, not by this input. The provider does not need
/// Lunar Artifact identity to transform bytes.
/// </summary>
public sealed record ImageArtifactInput : CapabilityExecutionInput
{
    public BinaryArtifactContent Content { get; }


    public ImageArtifactInput(BinaryArtifactContent content)
    {
        ArgumentNullException.ThrowIfNull(content);

        Content = content;
    }
}
