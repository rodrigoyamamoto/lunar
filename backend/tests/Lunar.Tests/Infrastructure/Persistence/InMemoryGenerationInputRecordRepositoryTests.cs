using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Infrastructure.Persistence;

public class InMemoryGenerationInputRecordRepositoryTests
{
    [Fact]
    public async Task TryAddAsync_FirstTime_Succeeds()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var record = CreateRecord();

        var result = await repository.TryAddAsync(record);

        Assert.True(result);
    }

    [Fact]
    public async Task TryAddAsync_DuplicateWorkflowExecutionId_ReturnsFalse()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var record = CreateRecord();
        await repository.TryAddAsync(record);

        var duplicate = new GenerationInputRecord(
            record.WorkflowExecutionId,
            record.AssetId,
            new TextPromptInput("different prompt"));

        var result = await repository.TryAddAsync(duplicate);

        Assert.False(result);
    }

    [Fact]
    public async Task TryAddAsync_Duplicate_DoesNotOverwriteOriginal()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var record = CreateRecord(prompt: "original prompt");
        await repository.TryAddAsync(record);

        var duplicate = new GenerationInputRecord(
            record.WorkflowExecutionId,
            record.AssetId,
            new TextPromptInput("overwriting prompt"));
        await repository.TryAddAsync(duplicate);

        var retrieved = (await repository.GetByAssetIdAsync(record.AssetId)).Single();
        Assert.Equal("original prompt", retrieved.Prompt.Prompt);
    }

    [Fact]
    public async Task GetByAssetIdAsync_ReturnsExactMatchingRecords()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var assetId = AssetId.New();
        var recordA = CreateRecord(assetId: assetId, prompt: "prompt A");
        var recordB = CreateRecord(assetId: assetId, prompt: "prompt B");
        await repository.TryAddAsync(recordA);
        await repository.TryAddAsync(recordB);

        var result = await repository.GetByAssetIdAsync(assetId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Prompt.Prompt == "prompt A");
        Assert.Contains(result, r => r.Prompt.Prompt == "prompt B");
    }

    [Fact]
    public async Task GetByAssetIdAsync_ExcludesOtherAssets()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var assetA = AssetId.New();
        var assetB = AssetId.New();
        await repository.TryAddAsync(CreateRecord(assetId: assetA, prompt: "for A"));
        await repository.TryAddAsync(CreateRecord(assetId: assetB, prompt: "for B"));

        var result = await repository.GetByAssetIdAsync(assetA);

        Assert.Single(result);
        Assert.Equal(assetA, result[0].AssetId);
        Assert.Equal("for A", result[0].Prompt.Prompt);
    }

    [Fact]
    public async Task GetByAssetIdAsync_ReturnedCollection_CannotMutateRepositoryState()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var assetId = AssetId.New();
        await repository.TryAddAsync(CreateRecord(assetId: assetId));

        var result = await repository.GetByAssetIdAsync(assetId);

        Assert.NotNull(result);
        Assert.True(((System.Collections.IList)result).IsReadOnly);

        var secondRecord = CreateRecord(assetId: assetId);
        Assert.Throws<NotSupportedException>(() =>
            ((System.Collections.IList)result).Add(secondRecord));

        var recordsAfterFailedMutation = await repository.GetByAssetIdAsync(assetId);
        Assert.Single(recordsAfterFailedMutation);
    }

    [Fact]
    public async Task TryAddAsync_PreCancelledToken_Propagates()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        var record = CreateRecord();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(record, cts.Token));
    }

    [Fact]
    public async Task GetByAssetIdAsync_PreCancelledToken_Propagates()
    {
        var repository = new InMemoryGenerationInputRecordRepository();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetByAssetIdAsync(AssetId.New(), cts.Token));
    }

    [Fact]
    public async Task GetByAssetIdAsync_EmptyAssetId_Throws()
    {
        var repository = new InMemoryGenerationInputRecordRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetByAssetIdAsync(new AssetId(Guid.Empty)));
    }

    [Fact]
    public async Task TryAddAsync_NullRecord_Throws()
    {
        var repository = new InMemoryGenerationInputRecordRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryAddAsync(null!));
    }


    private static GenerationInputRecord CreateRecord(
        AssetId? assetId = null,
        string prompt = "test prompt")
    {
        return new GenerationInputRecord(
            WorkflowExecutionId.New(),
            assetId ?? AssetId.New(),
            new TextPromptInput(prompt));
    }
}
