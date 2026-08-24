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
}
