using System.Diagnostics;
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
public class ActivityTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityId SharedCapabilityId = CapabilityId.New();
    private static readonly GenerationWorkflowTarget SharedTarget = new(SharedDefinitionId, 1, 1);


    [Fact]
    public async Task SuccessfulGeneration_EmitsGenerationActivityWithAssetId()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var generationActivity = listener.Activities.FirstOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);

        Assert.NotNull(generationActivity);
        Assert.Equal(SharedAssetId.Value.ToString(), generationActivity!.GetTagItem(ApplicationTelemetry.AssetIdTag)?.ToString());
    }


    [Fact]
    public async Task SuccessfulGeneration_EmitsGenerationActivityWithSuccessOutcome()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsSuccess);

        var generationActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);

        Assert.NotNull(generationActivity);
        var outcome = generationActivity!.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString();
        Assert.Equal(ApplicationTelemetry.OutcomeSuccess, outcome);
    }


    [Fact]
    public async Task SuccessfulGeneration_EmitsWorkflowGenerateAndStepExecuteActivities()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var operationNames = listener.Activities.Select(a => a.OperationName).ToList();
        Assert.Contains(ApplicationTelemetry.GenerationActivityName, operationNames);
        Assert.Contains(ApplicationTelemetry.WorkflowGenerateActivityName, operationNames);
        Assert.Contains(ApplicationTelemetry.WorkflowStepExecuteActivityName, operationNames);
        Assert.Contains(ApplicationTelemetry.CapabilityExecuteActivityName, operationNames);
    }


    [Fact]
    public async Task SuccessfulGeneration_EmitsContentAndMetadataPersistActivities()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var operationNames = listener.Activities.Select(a => a.OperationName).ToList();
        Assert.Contains(ApplicationTelemetry.ArtifactContentPersistActivityName, operationNames);
        Assert.Contains(ApplicationTelemetry.ArtifactMetadataPersistActivityName, operationNames);
    }


    [Fact]
    public async Task SuccessfulGeneration_NoPromptTextInActivityTags()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var prompt = "secret prompt that must not appear in trace tags";
        await service.GenerateAsync(SharedAssetId, new TextPromptInput(prompt));

        var allTagValues = listener.Activities
            .SelectMany(a => a.Tags)
            .Select(t => t.Value?.ToString() ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(prompt, string.Join("\n", allTagValues));
    }


    [Fact]
    public async Task MissingStep_GenerationActivityMarksFailureBeforeExecutionCreation()
    {
        using var listener = CreateListener();
        var missingStepTarget = new GenerationWorkflowTarget(SharedDefinitionId, 1, 99);
        var service = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            target: missingStepTarget);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);
        Assert.IsType<WorkflowStepNotFound>(result.Error);

        var generationActivity = listener.Activities.FirstOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);

        Assert.NotNull(generationActivity);
        Assert.Equal(ApplicationTelemetry.OutcomeFailure,
            generationActivity!.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString());

        var executionCreateActivities = listener.Activities.Where(
            a => a.OperationName == ApplicationTelemetry.WorkflowExecutionCreateActivityName).ToList();
        Assert.Empty(executionCreateActivities);
    }


    [Fact]
    public async Task ProviderFailure_CapabilityActivityMarksErrorWithFailureKind()
    {
        using var listener = CreateListener();
        var failingExecutor = new FailingExecutor();
        var service = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            executor: failingExecutor);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);

        var capabilityActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.CapabilityExecuteActivityName);

        Assert.NotNull(capabilityActivity);
        var failureKind = capabilityActivity!.GetTagItem(ApplicationTelemetry.FailureKindTag)?.ToString();
        Assert.False(string.IsNullOrEmpty(failureKind));
    }


    [Fact]
    public async Task Cancellation_GenerationActivityMarksCancelled()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(SharedAssetId, new TextPromptInput("test"), cts.Token));

        var generationActivity = listener.Activities.FirstOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);

        Assert.NotNull(generationActivity);
        Assert.Equal(ApplicationTelemetry.OutcomeCancelled,
            generationActivity!.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString());
    }


    [Fact]
    public async Task SuccessfulGeneration_EmitsExecutionCreateAndStartActivities()
    {
        using var listener = CreateListener();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var operationNames = listener.Activities.Select(a => a.OperationName).ToList();
        Assert.Contains(ApplicationTelemetry.WorkflowExecutionCreateActivityName, operationNames);
        Assert.Contains(ApplicationTelemetry.WorkflowExecutionStartActivityName, operationNames);
    }


    [Fact]
    public async Task ContentPersistenceFailure_MarksContentPersistErrorAndGenerationFailure()
    {
        using var listener = CreateListener();
        var rejectingContentStore = new RejectingContentStore();
        var service = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            contentStore: rejectingContentStore);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);

        var contentPersistActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.ArtifactContentPersistActivityName);
        Assert.NotNull(contentPersistActivity);
        Assert.Equal(ActivityStatusCode.Error, contentPersistActivity!.Status);

        var metadataPersistActivities = listener.Activities.Where(
            a => a.OperationName == ApplicationTelemetry.ArtifactMetadataPersistActivityName).ToList();
        Assert.Empty(metadataPersistActivities);

        var generationActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);
        Assert.NotNull(generationActivity);
        Assert.Equal(ApplicationTelemetry.OutcomeFailure,
            generationActivity!.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString());
        Assert.Equal(ApplicationTelemetry.StageArtifactContentPersistence,
            generationActivity.GetTagItem(ApplicationTelemetry.FailureStageTag)?.ToString());
    }


    [Fact]
    public async Task MetadataPersistenceFailure_MarksMetadataPersistErrorAndGenerationFailure()
    {
        using var listener = CreateListener();
        var rejectingArtifactRepository = new RejectingArtifactRepository();
        var service = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            artifactRepository: rejectingArtifactRepository);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);

        var contentPersistActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.ArtifactContentPersistActivityName);
        Assert.NotNull(contentPersistActivity);
        Assert.Equal(ActivityStatusCode.Ok, contentPersistActivity!.Status);

        var metadataPersistActivities = listener.Activities.Where(
            a => a.OperationName == ApplicationTelemetry.ArtifactMetadataPersistActivityName
                 && a.Status == ActivityStatusCode.Error).ToList();
        Assert.NotEmpty(metadataPersistActivities);
        var metadataPersistActivity = metadataPersistActivities.Last();

        var generationActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);
        Assert.NotNull(generationActivity);
        Assert.Equal(ApplicationTelemetry.OutcomeFailure,
            generationActivity!.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString());
        Assert.Equal(ApplicationTelemetry.StageArtifactMetadataPersistence,
            generationActivity.GetTagItem(ApplicationTelemetry.FailureStageTag)?.ToString());
    }


    [Fact]
    public async Task GenerationInputPersistenceFailure_MarksGenerationInputPersistenceStageAndGenerationFailure()
    {
        using var listener = CreateListener();
        var rejectingInputRepository = new RejectingGenerationInputRecordRepository();
        var service = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            generationInputRecordRepository: rejectingInputRepository);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);

        var generationActivity = listener.Activities.LastOrDefault(
            a => a.OperationName == ApplicationTelemetry.GenerationActivityName);
        Assert.NotNull(generationActivity);
        Assert.Equal(ActivityStatusCode.Error, generationActivity!.Status);
        Assert.Equal(ApplicationTelemetry.OutcomeFailure,
            generationActivity.GetTagItem(ApplicationTelemetry.OperationOutcomeTag)?.ToString());
        Assert.Equal(ApplicationTelemetry.StageGenerationInputPersistence,
            generationActivity.GetTagItem(ApplicationTelemetry.FailureStageTag)?.ToString());
        Assert.NotEqual(ApplicationTelemetry.StageWorkflowExecutionCreation,
            generationActivity.GetTagItem(ApplicationTelemetry.FailureStageTag)?.ToString());
    }


    private static TestActivityListener CreateListener()
    {
        return new TestActivityListener(
            ApplicationTelemetry.ActivitySourceName,
            InfrastructureTelemetry.ActivitySourceName);
    }


    private static async Task<GenerateDefaultArtifactService> CreateServiceAsync(
        bool withDefinition = false,
        bool withAsset = false,
        ICapabilityExecutor? executor = null,
        GenerationWorkflowTarget? target = null,
        IArtifactContentStore? contentStore = null,
        IArtifactRepository? artifactRepository = null,
        IGenerationInputRecordRepository? generationInputRecordRepository = null)
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var assetRepository = new InMemoryAssetRepository();
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        artifactRepository ??= new InMemoryArtifactRepository();
        contentStore ??= new InMemoryContentStore();
        executor ??= new TrackingExecutor();
        target ??= SharedTarget;
        generationInputRecordRepository ??= new InMemoryGenerationInputRecordRepository();

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
                new SingleCapabilityExecutorResolver(executor),
                contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            generationInputRecordRepository,
            NullLogger<GenerateArtifactService>.Instance);

        return new GenerateDefaultArtifactService(
            generateService,
            target,
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
            new BinaryArtifactContent(new byte[] { 0xFF, 0xD8 }, "image/jpeg"));

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }
    }


    private sealed class FailingExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionFailed(
                    new CapabilityExecutionFailure(
                        CapabilityExecutionFailureKind.RateLimited,
                        TimeSpan.FromSeconds(30))));
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


    private sealed class RejectingContentStore : IArtifactContentStore
    {
        public Task<bool> TryAddAsync(ArtifactId artifactId, ArtifactContent content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Task.FromResult(false);
        }

        public Task<ArtifactContent?> GetAsync(ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ArtifactContent?>(null);
        }

        public Task<bool> TryDeleteAsync(ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }
    }


    private sealed class RejectingArtifactRepository : IArtifactRepository
    {
        public Task<bool> TryAddAsync(Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<Artifact?> GetAsync(ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Artifact?>(null);
        }

        public Task<IReadOnlyList<Artifact>> GetByAssetIdAsync(AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<Artifact>>(Array.Empty<Artifact>());
        }
    }


    private sealed class RejectingGenerationInputRecordRepository : IGenerationInputRecordRepository
    {
        public Task<bool> TryAddAsync(
            GenerationInputRecord record,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<IReadOnlyList<GenerationInputRecord>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<GenerationInputRecord>>(
                Array.Empty<GenerationInputRecord>());
        }
    }
}
