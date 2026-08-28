using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;

namespace Lunar.Tests.Unit.Workflows;

public class GenerationInputRecordTests
{
    [Fact]
    public void Constructor_PreservesWorkflowExecutionId()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("a ruined sword");

        var record = new GenerationInputRecord(executionId, assetId, prompt);

        Assert.Equal(executionId, record.WorkflowExecutionId);
    }

    [Fact]
    public void Constructor_PreservesAssetId()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("a ruined sword");

        var record = new GenerationInputRecord(executionId, assetId, prompt);

        Assert.Equal(assetId, record.AssetId);
    }

    [Fact]
    public void Constructor_PreservesExactPrompt()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("  a ruined sword  with  internal  whitespace  ");

        var record = new GenerationInputRecord(executionId, assetId, prompt);

        Assert.Equal("  a ruined sword  with  internal  whitespace  ", record.Prompt.Prompt);
    }

    [Fact]
    public void Constructor_PreservesNewlinesInPrompt()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("line one\nline two\r\nline three");

        var record = new GenerationInputRecord(executionId, assetId, prompt);

        Assert.Equal("line one\nline two\r\nline three", record.Prompt.Prompt);
    }

    [Fact]
    public void Constructor_InitializesCreatedAt()
    {
        var record = new GenerationInputRecord(
            WorkflowExecutionId.New(),
            AssetId.New(),
            new TextPromptInput("test"));

        Assert.True(record.CreatedAt <= DateTimeOffset.UtcNow.AddSeconds(1));
        Assert.True(record.CreatedAt >= DateTimeOffset.UtcNow.AddSeconds(-5));
    }

    [Fact]
    public void Constructor_EmptyWorkflowExecutionId_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationInputRecord(
                new WorkflowExecutionId(Guid.Empty),
                AssetId.New(),
                new TextPromptInput("test")));
    }

    [Fact]
    public void Constructor_EmptyAssetId_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationInputRecord(
                WorkflowExecutionId.New(),
                new AssetId(Guid.Empty),
                new TextPromptInput("test")));
    }

    [Fact]
    public void Constructor_NullPrompt_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerationInputRecord(
                WorkflowExecutionId.New(),
                AssetId.New(),
                null!));
    }

    [Fact]
    public void Rehydrate_PreservesAllFieldsExactly()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("exact prompt");
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var record = GenerationInputRecord.Rehydrate(executionId, assetId, prompt, createdAt);

        Assert.Equal(executionId, record.WorkflowExecutionId);
        Assert.Equal(assetId, record.AssetId);
        Assert.Same(prompt, record.Prompt);
        Assert.Equal(createdAt, record.CreatedAt);
    }

    [Fact]
    public void Rehydrate_PreservesExactPromptWithInternalWhitespaceAndNewlines()
    {
        var executionId = WorkflowExecutionId.New();
        var assetId = AssetId.New();
        var prompt = new TextPromptInput("Ancient  Gate\nwith cold blue flame");
        var createdAt = new DateTimeOffset(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        var record = GenerationInputRecord.Rehydrate(executionId, assetId, prompt, createdAt);

        Assert.Equal("Ancient  Gate\nwith cold blue flame", record.Prompt.Prompt);
    }

    [Fact]
    public void Rehydrate_EmptyWorkflowExecutionId_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            GenerationInputRecord.Rehydrate(
                new WorkflowExecutionId(Guid.Empty),
                AssetId.New(),
                new TextPromptInput("test"),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rehydrate_EmptyAssetId_Rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            GenerationInputRecord.Rehydrate(
                WorkflowExecutionId.New(),
                new AssetId(Guid.Empty),
                new TextPromptInput("test"),
                DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Rehydrate_NullPrompt_Rejected()
    {
        Assert.Throws<ArgumentNullException>(() =>
            GenerationInputRecord.Rehydrate(
                WorkflowExecutionId.New(),
                AssetId.New(),
                null!,
                DateTimeOffset.UtcNow));
    }
}
