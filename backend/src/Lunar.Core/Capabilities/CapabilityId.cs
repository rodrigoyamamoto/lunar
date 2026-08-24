using Lunar.Core.Primitives;

namespace Lunar.Core.Capabilities;

public readonly record struct CapabilityId(Guid Value)
{
    public static CapabilityId New()
    {
        return new CapabilityId(IdGenerator.New());
    }
}
