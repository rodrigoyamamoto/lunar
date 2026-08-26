using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lunar.Core.Artifacts;

namespace Lunar.Infrastructure.FileSystem;

public sealed class LocalFileArtifactContentStore : IArtifactContentStore
{
    private const string ContentFileName = "content.bin";
    private const string MetadataFileName = "metadata.json";
    private const string TempDirectoryPrefix = ".tmp-";
    private const int SupportedSchemaVersion = 1;
    private const string BinaryContentKind = "binary";

    private static readonly JsonSerializerOptions MetadataJsonOptions = new()
    {
        WriteIndented = false
    };

    private readonly DirectoryInfo _root;


    public LocalFileArtifactContentStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException(
                "Root path cannot be null, empty, or whitespace.",
                nameof(rootPath));
        }

        var root = new DirectoryInfo(rootPath);

        if (!root.Exists)
        {
            root.Create();
        }

        _root = root;
    }


    public async Task<bool> TryAddAsync(
        ArtifactId artifactId,
        ArtifactContent content,
        CancellationToken cancellationToken = default)
    {
        if (artifactId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(artifactId));
        }

        ArgumentNullException.ThrowIfNull(content);

        cancellationToken.ThrowIfCancellationRequested();

        if (content is not BinaryArtifactContent binaryContent)
        {
            throw new NotSupportedException(
                "Only BinaryArtifactContent is supported by LocalFileArtifactContentStore.");
        }

        var finalDirectory = GetArtifactDirectory(artifactId);

        if (finalDirectory.Exists)
        {
            return false;
        }

        var tempDirectory = CreateUniqueTempDirectory();
        var published = false;

        try
        {
            await WriteContentFileAsync(tempDirectory, binaryContent, cancellationToken);
            await WriteMetadataFileAsync(tempDirectory, binaryContent, cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                Directory.Move(
                    tempDirectory.FullName,
                    finalDirectory.FullName);
            }
            catch (IOException)
            {
                if (Directory.Exists(finalDirectory.FullName))
                {
                    return false;
                }

                throw;
            }

            published = true;
            return true;
        }
        finally
        {
            if (!published)
            {
                CleanupTempDirectory(tempDirectory.FullName);
            }
        }
    }


    public async Task<ArtifactContent?> GetAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken = default)
    {
        if (artifactId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(artifactId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var finalDirectory = GetArtifactDirectory(artifactId);

        if (!finalDirectory.Exists)
        {
            return null;
        }

        var metadataFile = new FileInfo(Path.Combine(finalDirectory.FullName, MetadataFileName));
        var contentFile = new FileInfo(Path.Combine(finalDirectory.FullName, ContentFileName));

        if (!metadataFile.Exists)
        {
            throw new InvalidDataException(
                $"Artifact content metadata is missing for {artifactId}.");
        }

        if (!contentFile.Exists)
        {
            throw new InvalidDataException(
                $"Artifact content file is missing for {artifactId}.");
        }

        var metadata = await ReadMetadataAsync(metadataFile, cancellationToken);
        ValidateMetadata(metadata, artifactId);

        var bytes = await ReadContentBytesAsync(contentFile, cancellationToken);

        if (bytes.Length == 0)
        {
            throw new InvalidDataException(
                $"Artifact content file is empty for {artifactId}.");
        }

        return new BinaryArtifactContent(bytes, metadata.MediaType!);
    }


    public Task<bool> TryDeleteAsync(
        ArtifactId artifactId,
        CancellationToken cancellationToken = default)
    {
        if (artifactId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Artifact identifier cannot be empty.",
                nameof(artifactId));
        }

        cancellationToken.ThrowIfCancellationRequested();

        var finalDirectory = GetArtifactDirectory(artifactId);

        if (!finalDirectory.Exists)
        {
            return Task.FromResult(false);
        }

        finalDirectory.Delete(recursive: true);

        return Task.FromResult(true);
    }


    private DirectoryInfo GetArtifactDirectory(ArtifactId artifactId)
    {
        var dirName = artifactId.Value.ToString("N");
        return new DirectoryInfo(Path.Combine(_root.FullName, dirName));
    }


    private DirectoryInfo CreateUniqueTempDirectory()
    {
        string tempName;
        DirectoryInfo tempDir;

        do
        {
            tempName = TempDirectoryPrefix + Guid.NewGuid().ToString("N");
            tempDir = new DirectoryInfo(Path.Combine(_root.FullName, tempName));
        }
        while (tempDir.Exists);

        tempDir.Create();

        return tempDir;
    }


    private static async Task WriteContentFileAsync(
        DirectoryInfo directory,
        BinaryArtifactContent content,
        CancellationToken cancellationToken)
    {
        var contentPath = Path.Combine(directory.FullName, ContentFileName);

        await using var stream = new FileStream(
            contentPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await stream.WriteAsync(
            content.Data,
            cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }


    private static async Task WriteMetadataFileAsync(
        DirectoryInfo directory,
        BinaryArtifactContent content,
        CancellationToken cancellationToken)
    {
        var metadata = new DurableMetadata
        {
            SchemaVersion = SupportedSchemaVersion,
            ContentKind = BinaryContentKind,
            MediaType = content.MediaType
        };

        var metadataPath = Path.Combine(directory.FullName, MetadataFileName);

        await using var stream = new FileStream(
            metadataPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            useAsync: true);

        await JsonSerializer.SerializeAsync(
            stream,
            metadata,
            MetadataJsonOptions,
            cancellationToken);

        await stream.FlushAsync(cancellationToken);
    }


    private static async Task<DurableMetadata> ReadMetadataAsync(
        FileInfo metadataFile,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = metadataFile.OpenRead();

            return await JsonSerializer.DeserializeAsync<DurableMetadata>(
                stream,
                cancellationToken: cancellationToken)
                ?? throw new InvalidDataException(
                    "Artifact content metadata is null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Artifact content metadata is malformed.", ex);
        }
    }


    private static void ValidateMetadata(DurableMetadata metadata, ArtifactId artifactId)
    {
        if (metadata.SchemaVersion != SupportedSchemaVersion)
        {
            throw new InvalidDataException(
                $"Unsupported schema version {metadata.SchemaVersion} for {artifactId}. " +
                $"Expected {SupportedSchemaVersion}.");
        }

        if (metadata.ContentKind != BinaryContentKind)
        {
            throw new InvalidDataException(
                $"Unsupported content kind '{metadata.ContentKind}' for {artifactId}. " +
                $"Expected '{BinaryContentKind}'.");
        }

        if (string.IsNullOrWhiteSpace(metadata.MediaType))
        {
            throw new InvalidDataException(
                $"Media type is missing or blank in durable metadata for {artifactId}.");
        }
    }


    private static async Task<byte[]> ReadContentBytesAsync(
        FileInfo contentFile,
        CancellationToken cancellationToken)
    {
        await using var stream = contentFile.OpenRead();

        using var memoryStream = new MemoryStream();

        await stream.CopyToAsync(memoryStream, cancellationToken);

        return memoryStream.ToArray();
    }


    private static void CleanupTempDirectory(string tempPath)
    {
        if (!Directory.Exists(tempPath))
        {
            return;
        }

        Directory.Delete(tempPath, recursive: true);
    }


    private sealed class DurableMetadata
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; }

        [JsonPropertyName("contentKind")]
        public string ContentKind { get; set; } = string.Empty;

        [JsonPropertyName("mediaType")]
        public string MediaType { get; set; } = string.Empty;
    }
}
