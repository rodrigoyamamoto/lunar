using Lunar.Core.Assets;
using Lunar.Core.Capabilities;

namespace Lunar.Core.Workflows;

/// <summary>
/// Immutable provenance record capturing the exact user generation input
/// for one <see cref="WorkflowExecution"/>. The prompt is preserved exactly
/// as supplied: no trimming, whitespace collapse, case transformation, or
/// newline normalization is performed.
/// </summary>
public sealed class GenerationInputRecord
{
    public WorkflowExecutionId WorkflowExecutionId { get; }

    public AssetId AssetId { get; }

    public TextPromptInput Prompt { get; }

    public DateTimeOffset CreatedAt { get; }


    public GenerationInputRecord(
        WorkflowExecutionId workflowExecutionId,
        AssetId assetId,
        TextPromptInput prompt)
        : this(workflowExecutionId, assetId, prompt, DateTimeOffset.UtcNow)
    {
    }


    public static GenerationInputRecord Rehydrate(
        WorkflowExecutionId workflowExecutionId,
        AssetId assetId,
        TextPromptInput prompt,
        DateTimeOffset createdAt)
    {
        return new GenerationInputRecord(
            workflowExecutionId,
            assetId,
            prompt,
            createdAt);
    }


    private GenerationInputRecord(
        WorkflowExecutionId workflowExecutionId,
        AssetId assetId,
        TextPromptInput prompt,
        DateTimeOffset createdAt)
    {
        if (workflowExecutionId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Workflow execution identifier cannot be empty.",
                nameof(workflowExecutionId));
        }

        if (assetId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(assetId));
        }

        ArgumentNullException.ThrowIfNull(prompt);

        WorkflowExecutionId = workflowExecutionId;
        AssetId = assetId;
        Prompt = prompt;
        CreatedAt = createdAt;
    }
}
