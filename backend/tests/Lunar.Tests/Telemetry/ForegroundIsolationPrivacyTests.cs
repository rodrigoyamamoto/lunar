using System.Diagnostics;
using Lunar.Application.Assets;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Telemetry;

[Collection("Telemetry")]
public class ForegroundIsolationPrivacyTests
{
    private static readonly byte[] SecretImageBytes =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0x53, 0x45, 0x43, 0x52, 0x45, 0x54 };
    private static readonly byte[] PngOutputBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };

    private static readonly BinaryArtifactContent SecretJpegContent =
        new(SecretImageBytes, "image/jpeg");

    private static readonly BinaryArtifactContent PngContent =
        new(PngOutputBytes, "image/png");

    private static readonly string[] Sentinels =
    {
        "LUNAR_FOREGROUND_ISOLATION_TOKEN",
        "Authorization",
        "Bearer ",
        "data/artifacts",
        "LocalRootPath",
        "BindingInternalError",
        "IMAGES_SECRET_DETAIL",
    };


    [Fact]
    public async Task RemoveBackground_LogsNeverContainSourceImageBytes()
    {
        using var capture = await CreateServiceAsync();

        var secretHex = Convert.ToHexString(SecretImageBytes);
        var secretBase64 = Convert.ToBase64String(SecretImageBytes);

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values).Select(v => v?.ToString() ?? string.Empty));

        Assert.DoesNotContain(secretHex, allMessages);
        Assert.DoesNotContain(secretBase64, allMessages);
        Assert.DoesNotContain(secretHex, allProperties);
        Assert.DoesNotContain(secretBase64, allProperties);
        Assert.DoesNotContain("SECRET", allMessages);
    }

    [Fact]
    public async Task RemoveBackground_LogsNeverContainOutputImageBytes()
    {
        using var capture = await CreateServiceAsync();

        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));

        Assert.DoesNotContain(outputHex, allMessages);
        Assert.DoesNotContain(outputBase64, allMessages);
    }

    [Fact]
    public async Task RemoveBackground_LogsNeverContainServiceToken()
    {
        using var capture = await CreateServiceAsync();

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values).Select(v => v?.ToString() ?? string.Empty));

        Assert.DoesNotContain("LUNAR_FOREGROUND_ISOLATION_TOKEN", allMessages);
        Assert.DoesNotContain("LUNAR_FOREGROUND_ISOLATION_TOKEN", allProperties);
        Assert.DoesNotContain("Authorization", allMessages);
        Assert.DoesNotContain("Authorization", allProperties);
        Assert.DoesNotContain("Bearer ", allMessages);
        Assert.DoesNotContain("Bearer ", allProperties);
    }

    [Fact]
    public async Task RemoveBackground_LogsNeverContainLocalFileSystemPath()
    {
        using var capture = await CreateServiceAsync();

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values).Select(v => v?.ToString() ?? string.Empty));

        Assert.DoesNotContain("data/artifacts", allMessages);
        Assert.DoesNotContain("data/artifacts", allProperties);
        Assert.DoesNotContain("LocalRootPath", allMessages);
        Assert.DoesNotContain("LocalRootPath", allProperties);
    }

    [Fact]
    public async Task RemoveBackground_LogsNeverContainProviderExceptionMessage()
    {
        using var capture = await CreateServiceAsync();

        var allMessages = string.Join("\n", capture.LoggerProvider.Entries.Select(e => e.Message));
        var allProperties = string.Join("\n",
            capture.LoggerProvider.Entries.SelectMany(e => e.Properties.Values).Select(v => v?.ToString() ?? string.Empty));

        Assert.DoesNotContain("BindingInternalError", allMessages);
        Assert.DoesNotContain("BindingInternalError", allProperties);
        Assert.DoesNotContain("IMAGES_SECRET_DETAIL", allMessages);
        Assert.DoesNotContain("IMAGES_SECRET_DETAIL", allProperties);
    }

    [Fact]
    public async Task RemoveBackground_ActivityTagsNeverContainSentinels()
    {
        using var capture = await CreateServiceAsync();

        var allTagValues = string.Join("\n",
            capture.ActivityListener.Activities
                .SelectMany(a => a.Tags)
                .Select(t => t.Value?.ToString() ?? string.Empty));

        var secretHex = Convert.ToHexString(SecretImageBytes);
        var secretBase64 = Convert.ToBase64String(SecretImageBytes);
        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        Assert.DoesNotContain(secretHex, allTagValues);
        Assert.DoesNotContain(secretBase64, allTagValues);
        Assert.DoesNotContain(outputHex, allTagValues);
        Assert.DoesNotContain(outputBase64, allTagValues);

        foreach (var sentinel in Sentinels)
        {
            Assert.DoesNotContain(sentinel, allTagValues);
        }
    }

    [Fact]
    public async Task RemoveBackground_MetricTagsNeverContainSentinels()
    {
        using var capture = await CreateServiceAsync();

        var allTagValues = string.Join("\n",
            capture.MeterListener.AllMeasurements
                .SelectMany(m => m.Tags.Values)
                .Select(v => v?.ToString() ?? string.Empty));

        var secretHex = Convert.ToHexString(SecretImageBytes);
        var secretBase64 = Convert.ToBase64String(SecretImageBytes);
        var outputHex = Convert.ToHexString(PngOutputBytes);
        var outputBase64 = Convert.ToBase64String(PngOutputBytes);

        Assert.DoesNotContain(secretHex, allTagValues);
        Assert.DoesNotContain(secretBase64, allTagValues);
        Assert.DoesNotContain(outputHex, allTagValues);
        Assert.DoesNotContain(outputBase64, allTagValues);

        foreach (var sentinel in Sentinels)
        {
            Assert.DoesNotContain(sentinel, allTagValues);
        }
    }

    [Fact]
    public async Task RemoveBackground_ActivityEventsNeverContainSentinels()
    {
        using var capture = await CreateServiceAsync();

        var allEventPayloads = string.Join("\n",
            capture.ActivityListener.Activities
                .SelectMany(a => a.Events)
                .SelectMany(e => e.Tags)
                .Select(t => t.Value?.ToString() ?? string.Empty));

        foreach (var sentinel in Sentinels)
        {
            Assert.DoesNotContain(sentinel, allEventPayloads);
        }
    }


    private static async Task<TelemetryCapture> CreateServiceAsync()
    {
        var loggerProvider = new CaptureLoggerProvider();
        var activityListener = new TestActivityListener(
            InfrastructureTelemetry.ActivitySource.Name);
        var meterListener = new TestMeterListener(
            InfrastructureTelemetry.Meter.Name);

        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var assetRepository = new InMemoryAssetRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var contentStore = new InMemoryContentStore();
        var inputRecordRepo = new InMemoryGenerationInputRecordRepository();

        var target = new ForegroundIsolationWorkflowTarget(
            WorkflowDefinitionId.New(), 1, 1);

        await definitionRepository.TryAddAsync(new WorkflowDefinition(
            target.WorkflowDefinitionId,
            target.Version,
            "Foreground Isolation",
            new[] { new WorkflowStep(target.StepPosition, CapabilityId.New()) }));

        var assetId = AssetId.New();
        await assetRepository.TryAddAsync(new Asset(assetId, "Test", AssetType.Character));

        var sourceArtifact = new Artifact(
            ArtifactId.New(),
            assetId,
            "Test",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
        await artifactRepository.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, SecretJpegContent);

        var executor = new TransformExecutor(PngContent);

        var generateService = new GenerateArtifactService(
            definitionRepository,
            new CreateWorkflowExecutionService(
                assetRepository,
                definitionRepository,
                executionRepository,
                NullLogger<CreateWorkflowExecutionService>.Instance),
            new StartWorkflowExecutionService(
                executionRepository,
                NullLogger<StartWorkflowExecutionService>.Instance),
            new ExecuteWorkflowStepService(
                executionRepository,
                definitionRepository,
                artifactRepository,
                new SingleCapabilityExecutorResolver(executor),
                contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            inputRecordRepo,
            NullLogger<GenerateArtifactService>.Instance);

        var service = new RemoveArtifactBackgroundService(
            artifactRepository,
            contentStore,
            generateService,
            target,
            loggerProvider.CreateLogger<RemoveArtifactBackgroundService>());

        await service.RemoveBackgroundAsync(sourceArtifact.Id);

        return new TelemetryCapture(
            loggerProvider,
            activityListener,
            meterListener);
    }


    private sealed class TelemetryCapture : IDisposable
    {
        public CaptureLoggerProvider LoggerProvider { get; }
        public TestActivityListener ActivityListener { get; }
        public TestMeterListener MeterListener { get; }

        public TelemetryCapture(
            CaptureLoggerProvider loggerProvider,
            TestActivityListener activityListener,
            TestMeterListener meterListener)
        {
            LoggerProvider = loggerProvider;
            ActivityListener = activityListener;
            MeterListener = meterListener;
        }

        public void Dispose()
        {
            MeterListener.Dispose();
            ActivityListener.Dispose();
            LoggerProvider.Dispose();
        }
    }


    private sealed class TransformExecutor : ICapabilityExecutor
    {
        private readonly BinaryArtifactContent _outputContent;

        public TransformExecutor(BinaryArtifactContent outputContent)
        {
            _outputContent = outputContent;
        }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            var output = new CapabilityExecutionOutput(
            _outputContent);

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }
    }

    private sealed class InMemoryContentStore : IArtifactContentStore
    {
        private readonly Dictionary<ArtifactId, ArtifactContent> _store = new();

        public Task<bool> TryAddAsync(ArtifactId artifactId, ArtifactContent content, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryAdd(artifactId, content));
        }

        public Task<ArtifactContent?> GetAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.TryGetValue(artifactId, out var content) ? content : null);
        }

        public Task<bool> TryDeleteAsync(ArtifactId artifactId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Remove(artifactId));
        }
    }
}
