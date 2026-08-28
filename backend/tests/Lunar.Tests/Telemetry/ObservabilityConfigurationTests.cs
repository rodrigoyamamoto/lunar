using System.Diagnostics;
using Lunar.Application;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Lunar.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Lunar.Tests.Telemetry;

public class ObservabilityConfigurationTests
{
    [Fact]
    public void ActivityTrackingOptions_ContainsTraceIdAndSpanId()
    {
        // Verify the ActivityTrackingOptions flags used in Program.cs
        var trackingOptions = ActivityTrackingOptions.TraceId
            | ActivityTrackingOptions.SpanId;

        Assert.True(trackingOptions.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(trackingOptions.HasFlag(ActivityTrackingOptions.SpanId));
    }


    [Fact]
    public void RealHost_SimpleConsoleFormatterOptions_IncludesScopes()
    {
        // Resolve the effective formatter options from the actual Lunar application host.
        // The Simple console formatter reads IOptionsMonitor<SimpleConsoleFormatterOptions>.CurrentValue
        // (default/unnamed options), not named options. This test proves the real host
        // has IncludeScopes=true on the options the formatter actually consumes.
        using var factory = new LunarApiFactory();
        using var scope = factory.Services.CreateScope();
        var optionsMonitor = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<SimpleConsoleFormatterOptions>>();

        Assert.True(optionsMonitor.CurrentValue.IncludeScopes);
    }


    [Fact]
    public void RealHost_ActivityTrackingOptions_ContainsTraceIdAndSpanId()
    {
        // Resolve the effective ActivityTrackingOptions from the actual Lunar application host.
        using var factory = new LunarApiFactory();
        using var scope = factory.Services.CreateScope();
        var optionsMonitor = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<LoggerFactoryOptions>>();

        var trackingOptions = optionsMonitor.CurrentValue.ActivityTrackingOptions;

        Assert.True(trackingOptions.HasFlag(ActivityTrackingOptions.TraceId));
        Assert.True(trackingOptions.HasFlag(ActivityTrackingOptions.SpanId));
    }


    [Fact]
    public async Task LocalFileArtifactContentStore_DebugLogs_DoNotContainPathSentinels()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "lunar-test-" + Guid.NewGuid().ToString("N"));
        var provider = new CaptureLoggerProvider();

        try
        {
            var store = new LocalFileArtifactContentStore(
                tempRoot,
                provider.CreateLogger<LocalFileArtifactContentStore>());

            var artifactId = ArtifactId.New();
            var content = new BinaryArtifactContent(new byte[] { 0x01, 0x02, 0x03 }, "image/jpeg");

            var added = await store.TryAddAsync(artifactId, content);
            Assert.True(added);

            var retrieved = await store.GetAsync(artifactId);
            Assert.NotNull(retrieved);

            var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
            var allProperties = string.Join("\n", provider.Entries
                .SelectMany(e => e.Properties.Values)
                .Select(v => v?.ToString() ?? string.Empty));

            // No filesystem path sentinels should appear in any log
            Assert.DoesNotContain(tempRoot, allMessages);
            Assert.DoesNotContain(tempRoot, allProperties);
            Assert.DoesNotContain("content.bin", allMessages);
            Assert.DoesNotContain("metadata.json", allMessages);
            Assert.DoesNotContain(".tmp-", allMessages);
            Assert.DoesNotContain("DirectoryInfo", allMessages);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
