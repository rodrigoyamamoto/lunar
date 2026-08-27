using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Application.Workflows;

public class GenerateDefaultArtifactServiceTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityId SharedCapabilityId = CapabilityId.New();
    private static readonly TextPromptInput SharedInput = new("test prompt");
    private static readonly GenerationWorkflowTarget SharedTarget = new(SharedDefinitionId, 1, 1);


    [Fact]
    public void Constructor_NullGenerateArtifactService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateDefaultArtifactService(null!, SharedTarget, NullLogger<GenerateDefaultArtifactService>.Instance));
    }


    [Fact]
    public async Task Constructor_NullTarget_ShouldThrow()
    {
        var generateService = await CreateGenerateArtifactServiceAsync();

        Assert.Throws<ArgumentNullException>(() =>
            new GenerateDefaultArtifactService(generateService, null!, NullLogger<GenerateDefaultArtifactService>.Instance));
    }


    [Fact]
    public void Target_EmptyWorkflowDefinitionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationWorkflowTarget(new WorkflowDefinitionId(Guid.Empty), 1, 1));
    }


    [Fact]
    public void Target_VersionLessThanOne_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationWorkflowTarget(SharedDefinitionId, 0, 1));
    }


    [Fact]
    public void Target_StepPositionLessThanOne_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new GenerationWorkflowTarget(SharedDefinitionId, 1, 0));
    }


    [Fact]
    public async Task GenerateAsync_EmptyAssetId_ShouldThrow()
    {
        var service = await CreateServiceAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync(new AssetId(Guid.Empty), SharedInput));
    }


    [Fact]
    public async Task GenerateAsync_NullInput_ShouldThrow()
    {
        var service = await CreateServiceAsync();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.GenerateAsync(SharedAssetId, null!));
    }


    [Fact]
    public async Task GenerateAsync_Success_ReturnsGeneratedArtifact()
    {
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);

        var result = await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.WorkflowExecutionId.Value);
        Assert.NotNull(result.Value!.ProducedArtifact);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExactConfiguredWorkflowDefinitionIdReachesLowerService()
    {
        var executor = new TrackingExecutor();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true, executor: executor);

        await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.Equal(SharedDefinitionId, executor.LastRequest!.WorkflowDefinitionId);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExactConfiguredVersionReachesLowerService()
    {
        var executor = new TrackingExecutor();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true, executor: executor);

        await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.Equal(1, executor.LastRequest!.WorkflowDefinitionVersion);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExactConfiguredStepReachesLowerService()
    {
        var executor = new TrackingExecutor();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true, executor: executor);

        await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.Equal(1, executor.LastRequest!.StepPosition);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExactInputInstanceReachesLowerService()
    {
        var executor = new TrackingExecutor();
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true, executor: executor);
        var input = new TextPromptInput("a unique prompt");

        await service.GenerateAsync(SharedAssetId, input);

        Assert.Same(input, executor.LastRequest!.Input);
    }


    [Fact]
    public async Task GenerateAsync_MissingDefinition_PropagatesWorkflowDefinitionNotFound()
    {
        var service = await CreateServiceAsync(withDefinition: false, withAsset: true);

        var result = await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.True(result.IsFailure);
        Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
    }


    [Fact]
    public async Task GenerateAsync_MissingAsset_PropagatesAssetNotFound()
    {
        var service = await CreateServiceAsync(withDefinition: true, withAsset: false);

        var result = await service.GenerateAsync(SharedAssetId, SharedInput);

        Assert.True(result.IsFailure);
        Assert.IsType<AssetNotFound>(result.Error);
    }


    [Fact]
    public async Task GenerateAsync_PreCancelledToken_Propagates()
    {
        var service = await CreateServiceAsync(withDefinition: true, withAsset: true);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(SharedAssetId, SharedInput, cts.Token));
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
                assetRepository, definitionRepository, executionRepository,
                NullLogger<CreateWorkflowExecutionService>.Instance),
            new StartWorkflowExecutionService(
                executionRepository,
                NullLogger<StartWorkflowExecutionService>.Instance),
            new ExecuteWorkflowStepService(
                executionRepository, definitionRepository,
                artifactRepository, executor, contentStore,
                NullLogger<ExecuteWorkflowStepService>.Instance),
            NullLogger<GenerateArtifactService>.Instance);

        return new GenerateDefaultArtifactService(
            generateService, SharedTarget,
            NullLogger<GenerateDefaultArtifactService>.Instance);
    }

    private static async Task<GenerateArtifactService> CreateGenerateArtifactServiceAsync()
    {
        return new GenerateArtifactService(
            new InMemoryWorkflowDefinitionRepository(),
            new CreateWorkflowExecutionService(
                new InMemoryAssetRepository(),
                new InMemoryWorkflowDefinitionRepository(),
                new InMemoryWorkflowExecutionRepository(),
                NullLogger<CreateWorkflowExecutionService>.Instance),
            new StartWorkflowExecutionService(
                new InMemoryWorkflowExecutionRepository(),
                NullLogger<StartWorkflowExecutionService>.Instance),
            new ExecuteWorkflowStepService(
                new InMemoryWorkflowExecutionRepository(),
                new InMemoryWorkflowDefinitionRepository(),
                new InMemoryArtifactRepository(),
                new TrackingExecutor(),
                new InMemoryContentStore(),
                NullLogger<ExecuteWorkflowStepService>.Instance),
            NullLogger<GenerateArtifactService>.Instance);
    }


    private sealed class TrackingExecutor : ICapabilityExecutor
    {
        public int CallCount { get; private set; }
        public CapabilityExecutionRequest? LastRequest { get; private set; }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastRequest = request;

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
