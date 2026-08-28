using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class CapabilityExecutionOutputTests
{
    private static readonly ArtifactContent ValidContent =
        new BinaryArtifactContent(new byte[] { 0x00, 0x01, 0x7F }, "image/png");


    [Fact]
    public void Constructor_ValidContent_PreservesContent()
    {
        var output = new CapabilityExecutionOutput(ValidContent);

        Assert.Same(ValidContent, output.Content);
    }


    [Fact]
    public void Constructor_NullContent_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutionOutput(null!));
    }


    [Fact]
    public void Constructor_PreservesExactContentInstance()
    {
        var content = new BinaryArtifactContent(
            new byte[] { 0xFF, 0xFE, 0x00 },
            "image/webp");

        var output = new CapabilityExecutionOutput(content);

        Assert.Same(content, output.Content);
    }


    [Fact]
    public void OutputCarriesOnlyContent()
    {
        // After the metadata refactor, CapabilityExecutionOutput carries only
        // content. Name, type, and lineage are owned by the Application
        // workflow execution context, not by provider output.
        var output = new CapabilityExecutionOutput(ValidContent);

        var properties = output.GetType().GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "ArtifactName");
        Assert.DoesNotContain(properties, p => p.Name == "ArtifactType");
        Assert.DoesNotContain(properties, p => p.Name == "SourceArtifactIds");
    }
}
