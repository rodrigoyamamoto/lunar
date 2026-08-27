using Xunit;

namespace Lunar.Tests.Telemetry;

[CollectionDefinition("Telemetry")]
public class TelemetryCollection : ICollectionFixture<object>
{
    // This collection ensures telemetry tests that share static ActivitySource
    // and Meter instances are not run in parallel with each other.
}
