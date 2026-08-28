using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Telemetry;

[Collection("Telemetry")]
public class StructuredLogTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityId SharedCapabilityId = CapabilityId.New();
    private static readonly GenerationWorkflowTarget SharedTarget = new(SharedDefinitionId, 1, 1);


    [Fact]
    public async Task SuccessfulGeneration_LogsNoPromptText()
    {
        var (provider, service) = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var prompt = "a very specific secret prompt that must not appear in logs";
        await service.GenerateAsync(SharedAssetId, new TextPromptInput(prompt));

        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        Assert.DoesNotContain(prompt, allMessages);

        var allProperties = provider.Entries
            .SelectMany(e => e.Properties.Values)
            .Select(v => v?.ToString() ?? string.Empty);
        Assert.DoesNotContain(prompt, string.Join("\n", allProperties));
    }


    [Fact]
    public async Task SuccessfulGeneration_LogsStartedAndCompleted()
    {
        var (provider, service) = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var startedEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information && e.Message.Contains("Generation started"));
        Assert.NotNull(startedEntry);

        var completedEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Information && e.Message.Contains("Generation completed"));
        Assert.NotNull(completedEntry);
    }


    [Fact]
    public async Task ExpectedFailure_LogsWarningNotError()
    {
        var (provider, service) = await CreateServiceAsync(withDefinition: false, withAsset: true);

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);
        var errorEntries = provider.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Empty(errorEntries);

        var failedEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Generation failed"));
        Assert.NotNull(failedEntry);
    }


    [Fact]
    public async Task UnexpectedException_LogsExactlyOneErrorAndRethrows()
    {
        var (provider, service) = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            executor: new ThrowingExecutor());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateAsync(SharedAssetId, new TextPromptInput("test")));

        var errorEntries = provider.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Single(errorEntries);
        Assert.Contains("Generation crashed", errorEntries[0].Message);
    }


    [Fact]
    public async Task Cancellation_LogsWarningNotError()
    {
        var (provider, service) = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(SharedAssetId, new TextPromptInput("test"), cts.Token));

        var errorEntries = provider.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Empty(errorEntries);

        var cancelledEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Generation cancelled"));
        Assert.NotNull(cancelledEntry);
    }


    [Fact]
    public async Task ProviderSuccess_LogsNoCloudflareUrlOrAccountId()
    {
        var (provider, service) = await CreateServiceAsync(withDefinition: true, withAsset: true);

        await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        var allMessages = string.Join("\n", provider.Entries.Select(e => e.Message));
        Assert.DoesNotContain("api.cloudflare.com", allMessages);
        Assert.DoesNotContain("accounts/", allMessages);
    }


    [Fact]
    public async Task ProviderExpectedFailure_LogsWarningNotError()
    {
        var (provider, service) = await CreateServiceAsync(
            withDefinition: true,
            withAsset: true,
            executor: new FailingExecutor());

        var result = await service.GenerateAsync(SharedAssetId, new TextPromptInput("test"));

        Assert.True(result.IsFailure);

        // Application boundary should log Generation failed as Warning, not Error
        var errorEntries = provider.Entries.Where(e => e.Level == LogLevel.Error).ToList();
        Assert.Empty(errorEntries);

        var failedEntry = provider.Entries.FirstOrDefault(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("Generation failed"));
        Assert.NotNull(failedEntry);
    }


    private static async Task<(CaptureLoggerProvider Provider, GenerateDefaultArtifactService Service)> CreateServiceAsync(
        bool withDefinition = false,
        bool withAsset = false,
        ICapabilityExecutor? executor = null)
    {
        var provider = new CaptureLoggerProvider();
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
                new SingleCapabilityExecutorResolver(executor),
                contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            new InMemoryGenerationInputRecordRepository(),
            NullLogger<GenerateArtifactService>.Instance);

        var defaultService = new GenerateDefaultArtifactService(
            generateService,
            SharedTarget,
            provider.CreateLogger<GenerateDefaultArtifactService>());

        return (provider, defaultService);
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


    private sealed class ThrowingExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated unexpected failure");
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
}
