using Lunar.Core.Capabilities;

namespace Lunar.Tests.Unit.Capabilities;

public class TextPromptInputTests
{
    [Fact]
    public void Constructor_ValidPrompt_ShouldPreserveExactValue()
    {
        var prompt = "Generate a dark fantasy raven shrine.";

        var input = new TextPromptInput(prompt);

        Assert.Equal(prompt, input.Prompt);
    }


    [Fact]
    public void Constructor_NullPrompt_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() => new TextPromptInput(null!));
    }


    [Fact]
    public void Constructor_EmptyPrompt_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new TextPromptInput(""));
    }


    [Fact]
    public void Constructor_WhitespacePrompt_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new TextPromptInput("   "));
    }


    [Fact]
    public void Constructor_LeadingAndTrailingWhitespace_ShouldBePreservedExactly()
    {
        var prompt = "  moonlit raven shrine  ";

        var input = new TextPromptInput(prompt);

        Assert.Equal(prompt, input.Prompt);
    }


    [Fact]
    public void Constructor_InternalRepeatedWhitespace_ShouldBePreservedExactly()
    {
        var prompt = "ancient  raven   shrine";

        var input = new TextPromptInput(prompt);

        Assert.Equal(prompt, input.Prompt);
    }


    [Fact]
    public void Constructor_PromptWithPunctuationAndSpacing_ShouldBePreservedExactly()
    {
        var prompt = "  ancient raven shrine, moonlit -- cracked stone  ";

        var input = new TextPromptInput(prompt);

        Assert.Equal(prompt, input.Prompt);
    }


    [Fact]
    public void Constructor_ShouldBeCapabilityExecutionInput()
    {
        var input = new TextPromptInput("test prompt");

        Assert.IsAssignableFrom<CapabilityExecutionInput>(input);
    }
}
