using Lunar.Core.Artifacts;

namespace Lunar.Tests.Unit.Artifacts;

public class BinaryArtifactContentTests
{
    private static readonly byte[] ValidData = { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };


    [Fact]
    public void Constructor_ValidDataAndMediaType_ShouldPreserveBytesAndMediaType()
    {
        var content = new BinaryArtifactContent(ValidData, "image/png");

        Assert.Equal(ValidData, content.Data.ToArray());
        Assert.Equal("image/png", content.MediaType);
    }


    [Fact]
    public void Constructor_NullData_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BinaryArtifactContent(null!, "image/png"));
    }


    [Fact]
    public void Constructor_NullMediaType_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new BinaryArtifactContent(ValidData, null!));
    }


    [Fact]
    public void Constructor_EmptyMediaType_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BinaryArtifactContent(ValidData, ""));
    }


    [Fact]
    public void Constructor_WhitespaceMediaType_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BinaryArtifactContent(ValidData, "   "));
    }


    [Fact]
    public void Constructor_ZeroLengthData_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new BinaryArtifactContent(Array.Empty<byte>(), "image/png"));
    }


    [Fact]
    public void Constructor_CallerMutatesSourceBytes_ShouldNotAlterContent()
    {
        var source = new byte[] { 0x10, 0x20, 0x30 };
        var content = new BinaryArtifactContent(source, "image/png");

        source[0] = 0xFF;
        source[1] = 0xFF;
        source[2] = 0xFF;

        Assert.Equal(new byte[] { 0x10, 0x20, 0x30 }, content.Data.ToArray());
    }


    [Fact]
    public void Constructor_MediaTypeWithSpacingAndCasing_ShouldBePreservedExactly()
    {
        var mediaType = "  image/PNG  ";

        var content = new BinaryArtifactContent(ValidData, mediaType);

        Assert.Equal(mediaType, content.MediaType);
    }


    [Fact]
    public void Constructor_ShouldBeArtifactContent()
    {
        var content = new BinaryArtifactContent(ValidData, "image/png");

        Assert.IsAssignableFrom<ArtifactContent>(content);
    }
}
