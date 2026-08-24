using Lunar.Core.Primitives;

namespace Lunar.Core.Artifacts;

public readonly record struct ArtifactId(Guid Value)
{
    public static ArtifactId New()
    {
        return new ArtifactId(IdGenerator.New());
    }
}