namespace Lunar.Core.Primitives;

public static class IdGenerator
{
    public static Guid New()
    {
        return Guid.CreateVersion7();
    }
}