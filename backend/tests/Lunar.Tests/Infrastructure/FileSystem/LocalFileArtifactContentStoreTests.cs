using System.IO;
using System.Text.Json;
using Lunar.Core.Artifacts;
using Lunar.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Infrastructure.FileSystem;

public class LocalFileArtifactContentStoreTests : IDisposable
{
    private readonly string _rootPath;

    private static readonly byte[] SampleBytes =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 };

    private const string SampleMediaType = "image/jpeg";


    public LocalFileArtifactContentStoreTests()
    {
        _rootPath = Path.Combine(
            Path.GetTempPath(),
            "lunar-tests-" + Guid.NewGuid().ToString("N"));
    }


    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
        {
            Directory.Delete(_rootPath, recursive: true);
        }
    }


    private LocalFileArtifactContentStore CreateStore() =>
        new(_rootPath, NullLogger<LocalFileArtifactContentStore>.Instance);

    private static BinaryArtifactContent CreateContent(byte[]? bytes = null) =>
        new(bytes ?? SampleBytes, SampleMediaType);

    private string GetArtifactDir(ArtifactId id) =>
        Path.Combine(_rootPath, id.Value.ToString("N"));


    // ---- Add / round trip ----

    [Fact]
    public async Task TryAddAsync_ValidBinaryContent_ShouldReturnTrue()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        var result = await store.TryAddAsync(id, content);

        Assert.True(result);
    }

    [Fact]
    public async Task GetAsync_AfterAdd_ShouldReturnExactBytes()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        await store.TryAddAsync(id, content);

        var retrieved = await store.GetAsync(id);

        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(content.Data.ToArray(), binary.Data.ToArray());
    }

    [Fact]
    public async Task GetAsync_AfterAdd_ShouldReturnExactMediaType()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        await store.TryAddAsync(id, content);

        var retrieved = await store.GetAsync(id);

        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(SampleMediaType, binary.MediaType);
    }

    [Fact]
    public async Task GetAsync_NewStoreInstanceSameRoot_ShouldRetrieveContent()
    {
        var id = ArtifactId.New();
        var content = CreateContent();

        await CreateStore().TryAddAsync(id, content);

        var newStore = CreateStore();
        var retrieved = await newStore.GetAsync(id);

        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(content.Data.ToArray(), binary.Data.ToArray());
        Assert.Equal(SampleMediaType, binary.MediaType);
    }

    [Fact]
    public async Task TryAddAsync_FinalDirectoryNameDerivedFromArtifactId()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        await store.TryAddAsync(id, content);

        var expectedDir = GetArtifactDir(id);
        Assert.True(Directory.Exists(expectedDir));
    }

    [Fact]
    public async Task TryAddAsync_ShouldCreateContentAndMetadataFiles()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var dir = GetArtifactDir(id);
        Assert.True(File.Exists(Path.Combine(dir, "content.bin")));
        Assert.True(File.Exists(Path.Combine(dir, "metadata.json")));
    }

    [Fact]
    public async Task TryAddAsync_MetadataShouldContainSchemaVersion()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var metadata = ReadMetadata(id);
        Assert.Equal(1, metadata.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public async Task TryAddAsync_MetadataShouldContainBinaryContentKind()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var metadata = ReadMetadata(id);
        Assert.Equal("binary", metadata.RootElement.GetProperty("contentKind").GetString());
    }

    [Fact]
    public async Task TryAddAsync_MetadataShouldContainExactMediaType()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var mediaType = "image/png";
        var content = new BinaryArtifactContent(SampleBytes, mediaType);

        await store.TryAddAsync(id, content);

        var metadata = ReadMetadata(id);
        Assert.Equal(mediaType, metadata.RootElement.GetProperty("mediaType").GetString());
    }

    [Fact]
    public async Task TryAddAsync_DurableContentShouldBeRawBytesNotBase64()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        await store.TryAddAsync(id, content);

        var contentPath = Path.Combine(GetArtifactDir(id), "content.bin");
        var fileBytes = await File.ReadAllBytesAsync(contentPath);

        Assert.Equal(content.Data.ToArray(), fileBytes);

        // Verify it's not a Base64 string representation
        var asText = System.Text.Encoding.ASCII.GetString(fileBytes);
        Assert.NotEqual(Convert.ToBase64String(content.Data.ToArray()), asText);
    }


    // ---- Duplicate / insert-only ----

    [Fact]
    public async Task TryAddAsync_SecondAddForSameId_ShouldReturnFalse()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        var second = await store.TryAddAsync(id, CreateContent());

        Assert.False(second);
    }

    [Fact]
    public async Task TryAddAsync_SecondAddShouldNotOverwriteOriginalBytes()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var originalBytes = new byte[] { 0x01, 0x02, 0x03 };
        var originalContent = new BinaryArtifactContent(originalBytes, "image/jpeg");

        await store.TryAddAsync(id, originalContent);
        await store.TryAddAsync(id, new BinaryArtifactContent(
            new byte[] { 0xFF, 0xFF, 0xFF }, "image/png"));

        var retrieved = await store.GetAsync(id);
        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(originalBytes, binary.Data.ToArray());
    }

    [Fact]
    public async Task TryAddAsync_SecondAddShouldNotOverwriteMediaType()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var originalContent = new BinaryArtifactContent(SampleBytes, "image/jpeg");

        await store.TryAddAsync(id, originalContent);
        await store.TryAddAsync(id, new BinaryArtifactContent(SampleBytes, "image/png"));

        var retrieved = await store.GetAsync(id);
        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal("image/jpeg", binary.MediaType);
    }

    [Fact]
    public async Task TryAddAsync_ConcurrentSameIdDistinctPayloads_ExactlyOneWinnerWithWinnerContent()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        var candidates = Enumerable.Range(0, 10)
            .Select(i => (
                Bytes: new byte[] { (byte)i, 0xFF, 0xD8, (byte)i },
                MediaType: $"image/test-{i}"))
            .ToArray();

        var tasks = candidates
            .Select(c => store.TryAddAsync(
                id,
                new BinaryArtifactContent(c.Bytes, c.MediaType)))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        var winnerIndices = results
            .Select((r, i) => (r, i))
            .Where(x => x.r)
            .Select(x => x.i)
            .ToArray();

        Assert.Single(winnerIndices);
        var winnerIndex = winnerIndices[0];
        var winner = candidates[winnerIndex];

        var retrieved = await store.GetAsync(id);
        var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
        Assert.Equal(winner.Bytes, binary.Data.ToArray());
        Assert.Equal(winner.MediaType, binary.MediaType);
    }

    [Fact]
    public async Task TryAddAsync_ConcurrentSameId_NoTempDirectoriesRemain()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => store.TryAddAsync(id, content))
            .ToArray();

        await Task.WhenAll(tasks);

        var tempDirs = Directory.GetDirectories(_rootPath)
            .Where(d => Path.GetFileName(d).StartsWith(".tmp-"))
            .ToArray();

        Assert.Empty(tempDirs);
    }


    // ---- Multi-ID concurrency ----

    [Fact]
    public async Task TryAddAsync_ConcurrentDifferentIds_ShouldPreserveCorrelation()
    {
        var store = CreateStore();

        var idsWithBytes = Enumerable.Range(0, 10)
            .Select(i => (
                Id: ArtifactId.New(),
                Bytes: new byte[] { (byte)i, 0xFF, 0xD8 }))
            .ToArray();

        var tasks = idsWithBytes
            .Select(x => store.TryAddAsync(
                x.Id,
                new BinaryArtifactContent(x.Bytes, "image/jpeg")))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, Assert.True);

        foreach (var (id, bytes) in idsWithBytes)
        {
            var retrieved = await store.GetAsync(id);
            var binary = Assert.IsType<BinaryArtifactContent>(retrieved);
            Assert.Equal(bytes, binary.Data.ToArray());
        }
    }


    // ---- Missing / deletion ----

    [Fact]
    public async Task GetAsync_MissingId_ShouldReturnNull()
    {
        var store = CreateStore();

        var result = await store.GetAsync(ArtifactId.New());

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDeleteAsync_MissingId_ShouldReturnFalse()
    {
        var store = CreateStore();

        var result = await store.TryDeleteAsync(ArtifactId.New());

        Assert.False(result);
    }

    [Fact]
    public async Task TryDeleteAsync_ExistingId_ShouldReturnTrue()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        var result = await store.TryDeleteAsync(id);

        Assert.True(result);
    }

    [Fact]
    public async Task GetAsync_AfterDelete_ShouldReturnNull()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        await store.TryDeleteAsync(id);

        var result = await store.GetAsync(id);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryDeleteAsync_OneId_ShouldNotDeleteSibling()
    {
        var store = CreateStore();
        var id1 = ArtifactId.New();
        var id2 = ArtifactId.New();

        await store.TryAddAsync(id1, CreateContent());
        await store.TryAddAsync(id2, CreateContent());
        await store.TryDeleteAsync(id1);

        var remaining = await store.GetAsync(id2);
        Assert.NotNull(remaining);
        Assert.False(Directory.Exists(GetArtifactDir(id1)));
    }


    // ---- Validation ----

    [Fact]
    public async Task TryAddAsync_EmptyArtifactId_ShouldThrow()
    {
        var store = CreateStore();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TryAddAsync(new ArtifactId(Guid.Empty), CreateContent()));
    }

    [Fact]
    public async Task TryAddAsync_NullContent_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.TryAddAsync(id, null!));
    }

    [Fact]
    public async Task TryAddAsync_UnsupportedContentSubtype_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await Assert.ThrowsAsync<NotSupportedException>(() =>
            store.TryAddAsync(id, new UnsupportedTestContent()));
    }

    [Fact]
    public void Constructor_EmptyRootPath_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new LocalFileArtifactContentStore("", NullLogger<LocalFileArtifactContentStore>.Instance));
        Assert.Throws<ArgumentException>(() => new LocalFileArtifactContentStore("   ", NullLogger<LocalFileArtifactContentStore>.Instance));
    }


    // ---- Corrupt durable state ----

    [Fact]
    public async Task GetAsync_MissingMetadataFile_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        File.Delete(Path.Combine(GetArtifactDir(id), "metadata.json"));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_MissingContentFile_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        File.Delete(Path.Combine(GetArtifactDir(id), "content.bin"));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_MalformedMetadataJson_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var metadataPath = Path.Combine(GetArtifactDir(id), "metadata.json");
        await File.WriteAllTextAsync(metadataPath, "not valid json");

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_UnsupportedSchemaVersion_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        WriteMetadata(id, new { schemaVersion = 99, contentKind = "binary", mediaType = "image/jpeg" });

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_UnsupportedContentKind_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        WriteMetadata(id, new { schemaVersion = 1, contentKind = "unknown", mediaType = "image/jpeg" });

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_BlankMediaType_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        WriteMetadata(id, new { schemaVersion = 1, contentKind = "binary", mediaType = "   " });

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }

    [Fact]
    public async Task GetAsync_EmptyContentFile_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var contentPath = Path.Combine(GetArtifactDir(id), "content.bin");
        await File.WriteAllBytesAsync(contentPath, Array.Empty<byte>());

        await Assert.ThrowsAsync<InvalidDataException>(() => store.GetAsync(id));
    }


    // ---- Cancellation / publication ----

    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldThrowAndLeaveNoFinalEntry()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.TryAddAsync(id, CreateContent(), cts.Token));

        Assert.False(Directory.Exists(GetArtifactDir(id)));
    }

    [Fact]
    public async Task TryAddAsync_SuccessfulAdd_IsRetrievableAndNotConvertedToCancellation()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        var content = CreateContent();

        var result = await store.TryAddAsync(id, content);

        Assert.True(result);
        var retrieved = await store.GetAsync(id);
        Assert.NotNull(retrieved);
    }

    [Fact]
    public async Task GetAsync_PreCancelledToken_ShouldThrow()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        await store.TryAddAsync(id, CreateContent());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.GetAsync(id, cts.Token));
    }

    [Fact]
    public async Task TryDeleteAsync_PreCancelledToken_ShouldThrowAndLeaveEntryIntact()
    {
        var store = CreateStore();
        var id = ArtifactId.New();
        await store.TryAddAsync(id, CreateContent());

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.TryDeleteAsync(id, cts.Token));

        Assert.True(Directory.Exists(GetArtifactDir(id)));
    }


    // ---- Path safety ----

    [Fact]
    public async Task TryAddAsync_FinalPathUsesOnlyArtifactIdAndFixedFilenames()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var dirName = id.Value.ToString("N");
        var files = Directory.GetFiles(GetArtifactDir(id))
            .Select(Path.GetFileName)
            .OrderBy(f => f)
            .ToArray();

        Assert.Equal(new[] { "content.bin", "metadata.json" }, files);
        Assert.Equal(dirName, new DirectoryInfo(GetArtifactDir(id)).Name);
    }

    [Fact]
    public async Task TryAddAsync_NoTempDirectoriesRemainAfterSuccess()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());

        var tempDirs = Directory.GetDirectories(_rootPath)
            .Where(d => Path.GetFileName(d).StartsWith(".tmp-"))
            .ToArray();

        Assert.Empty(tempDirs);
    }

    [Fact]
    public async Task TryAddAsync_NoTempDirectoriesRemainAfterDuplicate()
    {
        var store = CreateStore();
        var id = ArtifactId.New();

        await store.TryAddAsync(id, CreateContent());
        await store.TryAddAsync(id, CreateContent());

        var tempDirs = Directory.GetDirectories(_rootPath)
            .Where(d => Path.GetFileName(d).StartsWith(".tmp-"))
            .ToArray();

        Assert.Empty(tempDirs);
    }


    // ---- Helpers ----

    private JsonDocument ReadMetadata(ArtifactId id)
    {
        var path = Path.Combine(GetArtifactDir(id), "metadata.json");
        var json = File.ReadAllText(path);
        return JsonDocument.Parse(json);
    }

    private void WriteMetadata(ArtifactId id, object metadata)
    {
        var path = Path.Combine(GetArtifactDir(id), "metadata.json");
        var json = JsonSerializer.Serialize(metadata);
        File.WriteAllText(path, json);
    }

    private sealed record UnsupportedTestContent : ArtifactContent;
}
