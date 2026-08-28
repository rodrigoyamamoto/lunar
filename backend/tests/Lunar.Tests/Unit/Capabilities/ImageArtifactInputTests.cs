using Lunar.Core.Artifacts;
using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class ImageArtifactInputTests
{
    private static readonly BinaryArtifactContent ValidContent =
        new(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg");


    [Fact]
    public void Constructor_ValidContent_PreservesContent()
    {
        var input = new ImageArtifactInput(ValidContent);

        Assert.Same(ValidContent, input.Content);
    }


    [Fact]
    public void Constructor_NullContent_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ImageArtifactInput(null!));
    }


    [Fact]
    public void Constructor_PreservesExactContentBytes()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };
        var content = new BinaryArtifactContent(bytes, "image/png");
        var input = new ImageArtifactInput(content);

        Assert.Equal("image/png", input.Content.MediaType);
        Assert.Equal(bytes.Length, input.Content.Data.Length);
    }


    [Fact]
    public void IsCapabilityExecutionInput()
    {
        var input = new ImageArtifactInput(ValidContent);

        Assert.IsAssignableFrom<CapabilityExecutionInput>(input);
    }


    [Fact]
    public void DoesNotCarrySourceArtifactId()
    {
        // After the lineage refactor, ImageArtifactInput no longer carries
        // a SourceArtifactId. The provider only needs image bytes and
        // media type. Direct Artifact lineage is owned by the Application
        // workflow execution context.
        var input = new ImageArtifactInput(ValidContent);

        var properties = input.GetType().GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "SourceArtifactId");
    }
}
