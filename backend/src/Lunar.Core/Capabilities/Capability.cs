namespace Lunar.Core.Capabilities;

public sealed class Capability
{
    public CapabilityId Id { get; }

    public string Name { get; }


    public Capability(
        CapabilityId id,
        string name)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Capability identifier cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Capability name cannot be null, empty, or whitespace.",
                nameof(name));
        }

        Id = id;
        Name = name;
    }
}
