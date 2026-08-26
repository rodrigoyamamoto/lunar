namespace Lunar.Core.Artifacts;

public sealed record BinaryArtifactContent : ArtifactContent
{
    public ReadOnlyMemory<byte> Data { get; }

    public string MediaType { get; }


    public BinaryArtifactContent(byte[] data, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(mediaType);

        if (data.Length == 0)
        {
            throw new ArgumentException(
                "Data must contain at least one byte.",
                nameof(data));
        }

        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new ArgumentException(
                "Media type cannot be empty or whitespace.",
                nameof(mediaType));
        }

        var ownedCopy = new byte[data.Length];
        data.CopyTo(ownedCopy, 0);

        Data = ownedCopy;
        MediaType = mediaType;
    }
}
