namespace Lunar.Core.Artifacts;

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId New()
    {
        return new(Guid.CreateVersion7());
    }
}