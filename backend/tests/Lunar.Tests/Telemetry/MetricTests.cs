using System.Diagnostics.Metrics;
using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure;
using Lunar.Infrastructure.Persistence;

using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Telemetry;

[Collection("Telemetry")]
public class MetricTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityId SharedCapabilityId = CapabilityId.New();
    private static readonly GenerationWorkflowTarget SharedTarget = new(SharedDefinitionId, 1, 1);


    [Fact]
    public async Task SuccessfulGeneration_RecordsOneAttempt()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var attempts = meterListener.GetCounterValues("lunar.generation.attempts");
        Assert.NotEmpty(attempts);
        Assert.Equal(1L, attempts.Sum(v => (long)v.Value));
    }


    [Fact]
    public async Task SuccessfulGeneration_RecordsDuration()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var durations = meterListener.GetHistogramValues("lunar.generation.duration");
        Assert.NotEmpty(durations);
        Assert.True(durations.All(v => v.Value >= 0));
    }


    [Fact]
    public async Task FailedGeneration_UsesOutcomeFailure()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: false, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var attempts = meterListener.GetCounterValues("lunar.generation.attempts");
        Assert.NotEmpty(attempts);

        var failureAttempts = attempts.Where(kvp =>
            kvp.Tags.TryGetValue(ApplicationTelemetry.OutcomeTag, out var outcome) &&
            outcome?.ToString() == ApplicationTelemetry.OutcomeFailure);

        Assert.NotEmpty(failureAttempts);
    }


    [Fact]
    public async Task CancelledGeneration_UsesOutcomeCancelled()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(SharedAssetId, new TextPromptInput("test"), cts.Token));

        var durations = meterListener.GetHistogramValues("lunar.generation.duration");
        var cancelledDurations = durations.Where(v =>
            v.Tags.TryGetValue(ApplicationTelemetry.OutcomeTag, out var outcome) &&
            outcome?.ToString() == ApplicationTelemetry.OutcomeCancelled);

        Assert.NotEmpty(cancelledDurations);
    }


    [Fact]
    public async Task SuccessfulGeneration_RecordsCapabilityDuration()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var durations = meterListener.GetHistogramValues("lunar.capability.execution.duration");
        Assert.NotEmpty(durations);
        Assert.True(durations.All(v => v.Value >= 0));
    }


    [Fact]
    public async Task SuccessfulGeneration_RecordsContentPersistenceDuration()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var durations = meterListener.GetHistogramValues("lunar.artifact.content.persistence.duration");
        Assert.NotEmpty(durations);
        Assert.True(durations.All(v => v.Value >= 0));
    }


    [Fact]
    public async Task Metrics_DoNotUseHighCardinalityTags()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var forbiddenSubstrings = new[]
        {
            "lunar.asset.id",
            "lunar.artifact.id",
            "lunar.workflow.execution.id",
            "lunar.workflow.definition.id",
            "lunar.capability.id",
            "trace",
            "span",
            "prompt"
        };

        foreach (var measurement in meterListener.AllMeasurements)
        {
            foreach (var tag in measurement.Tags)
            {
                var keyLower = tag.Key.ToLowerInvariant();
                foreach (var forbidden in forbiddenSubstrings)
                {
                    Assert.DoesNotContain(forbidden, keyLower);
                }
            }
        }
    }


    [Fact]
    public async Task CapabilityMetrics_UseBoundedOutcomeTag()
    {
        using var meterListener = CreateMeterListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var capabilityDurations = meterListener.GetHistogramValues("lunar.capability.execution.duration");
        Assert.NotEmpty(capabilityDurations);

        foreach (var record in capabilityDurations)
        {
            Assert.True(record.Tags.TryGetValue(ApplicationTelemetry.OutcomeTag, out var outcome));
            Assert.Equal(ApplicationTelemetry.OutcomeSuccess, outcome?.ToString());
        }
    }


    private static TestMeterListener CreateMeterListener()
    {
        return new TestMeterListener(
            ApplicationTelemetry.MeterName,
            InfrastructureTelemetry.MeterName);
    }


    private static async Task<GenerateDefaultArtifactService> CreateServiceAsync(
        bool withDefinition = false,
        bool withAsset = false,
        ICapabilityExecutor? executor = null)
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var assetRepository = new InMemoryAssetRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var contentStore = new InMemoryContentStore();
        executor ??= new TrackingExecutor();

        if (withDefinition)
        {
            await definitionRepository.TryAddAsync(new WorkflowDefinition(
                SharedDefinitionId,
                1,
                "Test",
                new[] { new WorkflowStep(1, SharedCapabilityId) }));
        }

        if (withAsset)
        {
            await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));
        }

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
                executor,
                contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<GenerateArtifactService>.Instance);

        return new GenerateDefaultArtifactService(
            generateService,
            SharedTarget,
            NullLogger<GenerateDefaultArtifactService>.Instance);
    }


    private sealed class TrackingExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var output = new CapabilityExecutionOutput(
                "output.jpg",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>(),
                new BinaryArtifactContent(new byte[] { 0xFF, 0xD8 }, "image/jpeg"));

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }
    }


    private sealed class InMemoryContentStore : IArtifactContentStore
    {
        private readonly Dictionary<ArtifactId, ArtifactContent> _store = new();

        public Task<bool> TryAddAsync(ArtifactId artifactId, ArtifactContent content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Task.FromResult(_store.TryAdd(artifactId, content));
        }

        public Task<ArtifactContent?> GetAsync(ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(artifactId, out var content);
            return Task.FromResult(content);
        }

        public Task<bool> TryDeleteAsync(ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Remove(artifactId));
        }
    }
}
