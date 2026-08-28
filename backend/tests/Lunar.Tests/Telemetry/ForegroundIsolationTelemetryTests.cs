using System.Diagnostics;
using Lunar.Application;
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
public class ForegroundIsolationTelemetryTests
{
    private static readonly byte[] SourceImageBytes =
        { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04 };
    private static readonly byte[] PngOutputBytes =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01 };

    private static readonly BinaryArtifactContent SourceJpegContent =
        new(SourceImageBytes, "image/jpeg");

    private static readonly BinaryArtifactContent PngContent =
        new(PngOutputBytes, "image/png");


    [Fact]
    public async Task RemoveBackground_Success_DurationMsIsNonNegativeElapsed()
    {
        using var capture = await CreateServiceAndExecuteAsync();

        var completedLog = capture.LoggerProvider.Entries
            .FirstOrDefault(e => e.Message.Contains("Background removal completed"));

        Assert.NotNull(completedLog);

        // DurationMs must be present as a structured property
        Assert.True(completedLog!.Properties.ContainsKey("DurationMs"),
            "DurationMs must be a structured log property");

        var durationValue = completedLog.Properties["DurationMs"];
        Assert.NotNull(durationValue);

        var durationMs = Convert.ToDouble(durationValue);

        // Duration must be non-negative (fake-fast path, no sleeps)
        Assert.True(durationMs >= 0,
            $"DurationMs must be non-negative elapsed time, got {durationMs}");

        // Duration must NOT be a raw Stopwatch timestamp (those are
        // typically huge values in the billions range)
        Assert.True(durationMs < 1_000_000,
            $"DurationMs must be elapsed milliseconds, not a raw Stopwatch timestamp. Got {durationMs}");
    }

    [Fact]
    public async Task RemoveBackground_Success_SourceArtifactIdTagRemainsStable()
    {
        using var capture = await CreateServiceAndExecuteAsync();

        var removeBackgroundActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == "lunar.artifact.remove_background");

        Assert.NotNull(removeBackgroundActivity);

        // The source ArtifactId tag must remain stable as the source
        // identity throughout the operation — it must NOT be overwritten
        // to mean the derived Artifact at success.
        var sourceArtifactTag = removeBackgroundActivity!.Tags
            .FirstOrDefault(t => t.Key == ApplicationTelemetry.ArtifactIdTag);

        Assert.NotEqual(default, sourceArtifactTag);
        Assert.Equal(capture.SourceArtifactId.Value.ToString(), sourceArtifactTag.Value);
    }

    [Fact]
    public async Task RemoveBackground_Success_DerivedArtifactIdTagRecordedSeparately()
    {
        using var capture = await CreateServiceAndExecuteAsync();

        var removeBackgroundActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == "lunar.artifact.remove_background");

        Assert.NotNull(removeBackgroundActivity);

        // The derived ArtifactId must be recorded under a distinct tag,
        // not by overwriting the source ArtifactId tag.
        var derivedArtifactTag = removeBackgroundActivity!.Tags
            .FirstOrDefault(t => t.Key == ApplicationTelemetry.DerivedArtifactIdTag);

        Assert.NotEqual(default, derivedArtifactTag);
        Assert.NotNull(derivedArtifactTag.Value);
        Assert.NotEqual(capture.SourceArtifactId.Value.ToString(), derivedArtifactTag.Value!.ToString());
    }

    [Fact]
    public async Task RemoveBackground_Success_SourceAndDerivedTagsAreDistinct()
    {
        using var capture = await CreateServiceAndExecuteAsync();

        var removeBackgroundActivity = capture.ActivityListener.Activities
            .FirstOrDefault(a => a.DisplayName == "lunar.artifact.remove_background");

        Assert.NotNull(removeBackgroundActivity);

        var sourceTag = removeBackgroundActivity!.Tags
            .FirstOrDefault(t => t.Key == ApplicationTelemetry.ArtifactIdTag);
        var derivedTag = removeBackgroundActivity.Tags
            .FirstOrDefault(t => t.Key == ApplicationTelemetry.DerivedArtifactIdTag);

        Assert.NotEqual(default, sourceTag);
        Assert.NotEqual(default, derivedTag);
        Assert.NotEqual(sourceTag.Value, derivedTag.Value);
    }


    private static async Task<TelemetryCapture> CreateServiceAndExecuteAsync()
    {
        var loggerProvider = new CaptureLoggerProvider();
        var activityListener = new TestActivityListener(
            ApplicationTelemetry.ActivitySource.Name);
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
            "Test Source",
            ArtifactType.ConceptImage,
            Array.Empty<ArtifactId>());
        await artifactRepository.TryAddAsync(sourceArtifact);
        await contentStore.TryAddAsync(sourceArtifact.Id, SourceJpegContent);

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
            meterListener,
            sourceArtifact.Id);
    }


    private sealed class TelemetryCapture : IDisposable
    {
        public CaptureLoggerProvider LoggerProvider { get; }
        public TestActivityListener ActivityListener { get; }
        public TestMeterListener MeterListener { get; }
        public ArtifactId SourceArtifactId { get; }

        public TelemetryCapture(
            CaptureLoggerProvider loggerProvider,
            TestActivityListener activityListener,
            TestMeterListener meterListener,
            ArtifactId sourceArtifactId)
        {
            LoggerProvider = loggerProvider;
            ActivityListener = activityListener;
            MeterListener = meterListener;
            SourceArtifactId = sourceArtifactId;
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
            var output = new CapabilityExecutionOutput(_outputContent);
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
