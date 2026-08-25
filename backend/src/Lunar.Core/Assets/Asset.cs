namespace Lunar.Core.Assets;

public sealed class Asset
{
    public AssetId Id { get; }

    public string Name { get; }

    public AssetType Type { get; }

    public AssetStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; }


    public Asset(
        AssetId id,
        string name,
        AssetType type)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Asset name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        Id = id;
        Name = name;
        Type = type;
        Status = AssetStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }


    public void MarkAsProcessing()
    {
        if (Status is not (
            AssetStatus.Draft or
            AssetStatus.Completed or
            AssetStatus.Failed))
        {
            return;
        }

        Status = AssetStatus.Processing;
    }


    public void MarkAsCompleted()
    {
        if (Status != AssetStatus.Processing)
        {
            return;
        }

        Status = AssetStatus.Completed;
    }


    public void MarkAsFailed()
    {
        if (Status != AssetStatus.Processing)
        {
            return;
        }

        Status = AssetStatus.Failed;
    }


    public static Asset Rehydrate(
        AssetId id,
        string name,
        AssetType type,
        AssetStatus status,
        DateTimeOffset createdAt)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Asset identifier cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Asset name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        return new Asset(
            id,
            name,
            type,
            status,
            createdAt);
    }


    private Asset(
        AssetId id,
        string name,
        AssetType type,
        AssetStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Type = type;
        Status = status;
        CreatedAt = createdAt;
    }
}
