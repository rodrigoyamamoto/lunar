namespace Lunar.Core.Capabilities;

public sealed record TextPromptInput : CapabilityExecutionInput
{
    public string Prompt { get; }


    public TextPromptInput(string prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException(
                "Prompt cannot be empty or whitespace.",
                nameof(prompt));
        }

        Prompt = prompt;
    }
}
