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
        Id = id;
        Name = name;
        Type = type;
        Status = AssetStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
    }


    public void MarkAsProcessing()
    {
        Status = AssetStatus.Processing;
    }


    public void MarkAsCompleted()
    {
        Status = AssetStatus.Completed;
    }


    public void MarkAsFailed()
    {
        Status = AssetStatus.Failed;
    }
}