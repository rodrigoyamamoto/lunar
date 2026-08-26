using Lunar.Application;
using Lunar.Application.Artifacts;
using Lunar.Application.Errors;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Application.Workflows;

public class ExecuteWorkflowStepServiceTests
{
    private static AssetId SharedAssetId { get; } = AssetId.New();

    private static readonly CapabilityExecutionInput SharedInput =
        new TextPromptInput("Generate a dark fantasy raven shrine.");

    private static readonly ArtifactContent SharedContent =
        new BinaryArtifactContent(new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF }, "image/png");


    private static WorkflowDefinition CreateDefinition(
        WorkflowDefinitionId definitionId,
        int version,
        params WorkflowStep[] steps)
    {
        return new WorkflowDefinition(
            definitionId,
            version,
            $"Test Definition v{version}",
            steps);
    }


    private static async Task<WorkflowDefinition> PersistDefinitionAsync(
        IWorkflowDefinitionRepository repository,
        WorkflowDefinitionId definitionId,
        int version,
        params WorkflowStep[] steps)
    {
        var definition = CreateDefinition(definitionId, version, steps);
        await repository.TryAddAsync(definition);
        return await repository.GetAsync(definitionId, version)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistRunningExecutionAsync(
        IWorkflowExecutionRepository repository,
        AssetId? assetId = null,
        WorkflowDefinitionId? definitionId = null,
        int definitionVersion = 1)
    {
        var execution = WorkflowExecution.Create(
            assetId ?? SharedAssetId,
            definitionId ?? WorkflowDefinitionId.New(),
            definitionVersion);

        await repository.TryAddAsync(execution);
        execution.Start();
        await repository.TryUpdateAsync(execution, 0);
        return await repository.GetAsync(execution.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static async Task<WorkflowExecution> PersistTerminalExecutionAsync(
        IWorkflowExecutionRepository repository,
        WorkflowExecutionStatus terminalStatus,
        AssetId? assetId = null,
        WorkflowDefinitionId? definitionId = null,
        int definitionVersion = 1)
    {
        var running = await PersistRunningExecutionAsync(
            repository,
            assetId,
            definitionId,
            definitionVersion);

        var transitioned = WorkflowExecution.Rehydrate(
            running.Id,
            running.AssetId,
            running.WorkflowDefinitionId,
            running.WorkflowDefinitionVersion,
            terminalStatus,
            running.Revision,
            running.CreatedAt,
            running.StartedAt,
            DateTimeOffset.UtcNow);

        await repository.TryUpdateAsync(transitioned, running.Revision);
        return await repository.GetAsync(running.Id)
            ?? throw new InvalidOperationException("Test setup failed.");
    }


    private static ExecuteWorkflowStepService CreateService(
        IWorkflowExecutionRepository? executionRepository = null,
        IWorkflowDefinitionRepository? definitionRepository = null,
        IArtifactRepository? artifactRepository = null,
        ICapabilityExecutor? executor = null)
    {
        return new ExecuteWorkflowStepService(
            executionRepository ?? new InMemoryWorkflowExecutionRepository(),
            definitionRepository ?? new InMemoryWorkflowDefinitionRepository(),
            artifactRepository ?? new InMemoryArtifactRepository(),
            executor ?? new StubCapabilityExecutor());
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldReturnSuccessWithArtifact()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();
        var capabilityId = CapabilityId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, capabilityId));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor(
            "knight-concept.png",
            ArtifactType.ConceptImage);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository,
            executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotEqual(Guid.Empty, result.Value!.Artifact.Id.Value);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldUseExactDefinitionVersion()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        var v1Capability = CapabilityId.New();
        var v2Capability = CapabilityId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, v1Capability));

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            2,
            new WorkflowStep(1, v2Capability));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.NotNull(executor.CapturedRequest);
        Assert.Equal(v1Capability, executor.CapturedRequest!.CapabilityId);
        Assert.NotEqual(v2Capability, executor.CapturedRequest.CapabilityId);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldSendExactCapabilityIdToExecutor()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        var capabilityId = CapabilityId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, capabilityId));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.NotNull(executor.CapturedRequest);
        Assert.Equal(capabilityId, executor.CapturedRequest!.CapabilityId);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldSendExactExecutionContext()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        var capabilityId = CapabilityId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, capabilityId));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.NotNull(executor.CapturedRequest);
        var request = executor.CapturedRequest!;
        Assert.Equal(execution.AssetId, request.AssetId);
        Assert.Equal(execution.Id, request.WorkflowExecutionId);
        Assert.Equal(execution.WorkflowDefinitionId, request.WorkflowDefinitionId);
        Assert.Equal(execution.WorkflowDefinitionVersion, request.WorkflowDefinitionVersion);
        Assert.Equal(1, request.StepPosition);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldCreateArtifactWithLunarOwnedIdentity()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        var artifact = result.Value!.Artifact;
        Assert.NotEqual(Guid.Empty, artifact.Id.Value);
        Assert.Equal(execution.AssetId, artifact.AssetId);
        Assert.Equal(execution.Id, artifact.SourceExecutionId);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPreserveOutputNameTypeAndSourceArtifactIds()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();
        var sourceA = ArtifactId.New();
        var sourceB = ArtifactId.New();
        var sourceC = ArtifactId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor(
            "Ancient  Gate Texture",
            ArtifactType.Texture,
            new[] { sourceA, sourceB, sourceC });

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        var artifact = result.Value!.Artifact;
        Assert.Equal("Ancient  Gate Texture", artifact.Name);
        Assert.Equal(ArtifactType.Texture, artifact.Type);
        Assert.Equal(3, artifact.SourceArtifactIds.Count);
        Assert.Equal(sourceA, artifact.SourceArtifactIds[0]);
        Assert.Equal(sourceB, artifact.SourceArtifactIds[1]);
        Assert.Equal(sourceC, artifact.SourceArtifactIds[2]);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPersistArtifact()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor(
            "persisted-concept.png",
            ArtifactType.ConceptImage);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository,
            executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        var retrieved = await artifactRepository.GetAsync(result.Value!.Artifact.Id);
        Assert.NotNull(retrieved);
        Assert.Equal("persisted-concept.png", retrieved!.Name);
        Assert.Equal(ArtifactType.ConceptImage, retrieved.Type);
        Assert.Equal(execution.AssetId, retrieved.AssetId);
        Assert.Equal(execution.Id, retrieved.SourceExecutionId);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldReturnProducedArtifactWithExecutorContent()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var content = new BinaryArtifactContent(
            new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF },
            "image/png");
        var executor = new StubCapabilityExecutor(
            "knight-concept.png",
            ArtifactType.ConceptImage,
            content: content);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        Assert.IsType<ProducedArtifact>(result.Value);
        Assert.Same(content, result.Value!.Content);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPreserveExactBinaryContentBytes()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var expectedBytes = new byte[] { 0x00, 0x01, 0x7F, 0x80, 0xFE, 0xFF };
        var content = new BinaryArtifactContent(expectedBytes, "image/png");
        var executor = new StubCapabilityExecutor(
            "knight-concept.png",
            ArtifactType.ConceptImage,
            content: content);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        var binaryContent = Assert.IsType<BinaryArtifactContent>(result.Value!.Content);
        Assert.Equal(expectedBytes, binaryContent.Data.ToArray());
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPreserveExactMediaType()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var content = new BinaryArtifactContent(
            new byte[] { 0x00, 0x01 },
            "image/webp");
        var executor = new StubCapabilityExecutor(
            "knight-concept.png",
            ArtifactType.ConceptImage,
            content: content);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsSuccess);
        var binaryContent = Assert.IsType<BinaryArtifactContent>(result.Value!.Content);
        Assert.Equal("image/webp", binaryContent.MediaType);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldSendExactInputInstanceToExecutor()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var input = new TextPromptInput("Generate a dark fantasy raven shrine.");
        await service.ExecuteAsync(execution.Id, 1, input);

        Assert.NotNull(executor.CapturedRequest);
        Assert.Same(input, executor.CapturedRequest!.Input);
        var textPrompt = Assert.IsType<TextPromptInput>(executor.CapturedRequest.Input);
        Assert.Equal("Generate a dark fantasy raven shrine.", textPrompt.Prompt);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldPreserveExactPromptWithSpacingAndPunctuation()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: executor);

        var prompt = "  ancient raven shrine, moonlit -- cracked stone  ";
        var input = new TextPromptInput(prompt);
        await service.ExecuteAsync(execution.Id, 1, input);

        Assert.NotNull(executor.CapturedRequest);
        var textPrompt = Assert.IsType<TextPromptInput>(executor.CapturedRequest!.Input);
        Assert.Equal(prompt, textPrompt.Prompt);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_QuotaExhausted_ShouldReturnTypedFailureWithoutPersistence()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.QuotaExhausted));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(1, error.StepPosition);
        Assert.Equal(CapabilityExecutionFailureKind.QuotaExhausted, error.Kind);
        Assert.Null(error.RetryAfter);
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled,
            "Artifact persistence must not be attempted on executor failure.");
        Assert.Equal(1, executor.CallCount);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_TemporarilyUnavailable_ShouldReturnTypedFailureWithoutPersistence()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.TemporarilyUnavailable));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(CapabilityExecutionFailureKind.TemporarilyUnavailable, error.Kind);
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled);
        Assert.Equal(1, executor.CallCount);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_RemoteOutcomeUnknown_ShouldReturnTypedFailureWithoutPersistence()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.RemoteOutcomeUnknown));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(CapabilityExecutionFailureKind.RemoteOutcomeUnknown, error.Kind);
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled);
        Assert.Equal(1, executor.CallCount);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_InvalidResponse_ShouldReturnTypedFailureWithoutPersistence()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.InvalidResponse));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(CapabilityExecutionFailureKind.InvalidResponse, error.Kind);
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled);
        Assert.Equal(1, executor.CallCount);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_WithRetryAfter_ShouldPreserveRetryAfter()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var retryAfter = TimeSpan.FromSeconds(30);
        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(
                CapabilityExecutionFailureKind.RateLimited,
                retryAfter));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, error.Kind);
        Assert.Equal(retryAfter, error.RetryAfter);
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled);
    }


    [Fact]
    public async Task ExecuteAsync_FailedOutcome_ShouldNotMutateWorkflowExecution()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new FailingCapabilityExecutor(
            new CapabilityExecutionFailure(CapabilityExecutionFailureKind.QuotaExhausted));

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        await service.ExecuteAsync(execution.Id, 1, SharedInput);

        var unchanged = await executionRepository.GetAsync(execution.Id);
        Assert.NotNull(unchanged);
        Assert.Equal(WorkflowExecutionStatus.Running, unchanged!.Status);
        Assert.Equal(execution.Revision, unchanged.Revision);
    }


    [Fact]
    public void WorkflowStepExecutionFailed_EmptyWorkflowExecutionId_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStepExecutionFailed(
                new WorkflowExecutionId(Guid.Empty),
                1,
                new CapabilityExecutionFailure(CapabilityExecutionFailureKind.Rejected)));
    }


    [Fact]
    public void WorkflowStepExecutionFailed_ZeroStepPosition_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() =>
            new WorkflowStepExecutionFailed(
                WorkflowExecutionId.New(),
                0,
                new CapabilityExecutionFailure(CapabilityExecutionFailureKind.Rejected)));
    }


    [Fact]
    public void WorkflowStepExecutionFailed_NullFailure_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new WorkflowStepExecutionFailed(
                WorkflowExecutionId.New(),
                1,
                null!));
    }


    [Fact]
    public void WorkflowStepExecutionFailed_ValidConstruction_ShouldExposeFailureAndConvenienceProperties()
    {
        var failure = new CapabilityExecutionFailure(
            CapabilityExecutionFailureKind.RateLimited,
            TimeSpan.FromSeconds(30));

        var error = new WorkflowStepExecutionFailed(
            WorkflowExecutionId.New(),
            2,
            failure);

        Assert.Same(failure, error.Failure);
        Assert.Equal(CapabilityExecutionFailureKind.RateLimited, error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(30), error.RetryAfter);
    }


    [Fact]
    public async Task ExecuteAsync_NullOutcome_ShouldThrowInvalidOperationException()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            SharedAssetId,
            definitionId,
            1);

        var nullExecutor = new NullReturningCapabilityExecutor();

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: nullExecutor);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput));
    }


    [Fact]
    public async Task ExecuteAsync_NullInput_ShouldThrowBeforeRepositoryLookup()
    {
        var trackingExecutionRepository = new TrackingWorkflowExecutionRepository();
        var service = CreateService(executionRepository: trackingExecutionRepository);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ExecuteAsync(WorkflowExecutionId.New(), 1, null!));

        Assert.False(
            trackingExecutionRepository.GetAsyncWasCalled,
            "GetAsync must not be called when input is null.");
    }


    [Fact]
    public async Task ExecuteAsync_MissingExecution_ShouldReturnNotFoundWithExactId()
    {
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var service = CreateService(executor: trackingExecutor, artifactRepository: trackingArtifactRepository);

        var executionId = WorkflowExecutionId.New();

        var result = await service.ExecuteAsync(executionId, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowExecutionNotFound>(result.Error);
        Assert.Equal(executionId, error.WorkflowExecutionId);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when execution is missing.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when execution is missing.");
    }


    [Fact]
    public async Task ExecuteAsync_CreatedExecution_ShouldReturnNotRunning()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();

        var execution = WorkflowExecution.Create(
            SharedAssetId,
            WorkflowDefinitionId.New(),
            1);
        await executionRepository.TryAddAsync(execution);

        var service = CreateService(
            executionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowExecutionNotRunning>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(WorkflowExecutionStatus.Created, error.CurrentStatus);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when execution is not Running.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when execution is not Running.");
    }


    [Theory]
    [InlineData(WorkflowExecutionStatus.Completed)]
    [InlineData(WorkflowExecutionStatus.Failed)]
    [InlineData(WorkflowExecutionStatus.Cancelled)]
    public async Task ExecuteAsync_TerminalExecution_ShouldReturnNotRunning(
        WorkflowExecutionStatus terminalStatus)
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();

        var execution = await PersistTerminalExecutionAsync(
            executionRepository,
            terminalStatus);

        var service = CreateService(
            executionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowExecutionNotRunning>(result.Error);
        Assert.Equal(execution.Id, error.WorkflowExecutionId);
        Assert.Equal(terminalStatus, error.CurrentStatus);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when execution is terminal.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when execution is terminal.");
    }


    [Fact]
    public async Task ExecuteAsync_MissingDefinition_ShouldReturnDefinitionNotFoundWithExactIdAndVersion()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var service = CreateService(
            executionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
        Assert.Equal(definitionId, error.WorkflowDefinitionId);
        Assert.Equal(1, error.Version);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when definition is missing.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when definition is missing.");
    }


    [Fact]
    public async Task ExecuteAsync_NoLatestVersionFallback_ShouldReturnDefinitionNotFound()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            2,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
        Assert.Equal(definitionId, error.WorkflowDefinitionId);
        Assert.Equal(1, error.Version);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when exact version is missing.");
    }


    [Fact]
    public async Task ExecuteAsync_MissingStepPosition_ShouldReturnStepNotFound()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()),
            new WorkflowStep(2, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 3, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepNotFound>(result.Error);
        Assert.Equal(definitionId, error.WorkflowDefinitionId);
        Assert.Equal(1, error.WorkflowDefinitionVersion);
        Assert.Equal(3, error.StepPosition);
        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when step is missing.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when step is missing.");
    }


    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task ExecuteAsync_InvalidStepPosition_ShouldThrowBeforeRepositoryLookup(
        int stepPosition)
    {
        var trackingExecutionRepository = new TrackingWorkflowExecutionRepository();
        var service = CreateService(executionRepository: trackingExecutionRepository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(WorkflowExecutionId.New(), stepPosition, SharedInput));

        Assert.False(
            trackingExecutionRepository.GetAsyncWasCalled,
            "GetAsync must not be called when step position is invalid.");
    }


    [Fact]
    public async Task ExecuteAsync_ExecutorException_ShouldPropagateUnchanged()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var expectedException = new InvalidOperationException("Provider crashed.");
        var throwingExecutor = new ThrowingCapabilityExecutor(expectedException);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: throwingExecutor);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput));

        Assert.Same(expectedException, actual);
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "Persistence must not be called when executor throws.");
    }


    [Fact]
    public async Task ExecuteAsync_EmptyArtifactName_ShouldFailAtArtifactConstruction()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor(
            "",
            ArtifactType.ConceptImage);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput));

        Assert.True(
            executor.ExecuteAsyncWasCalled,
            "Executor must have been reached before Artifact construction.");
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "Persistence must not be called when Artifact construction fails.");
    }


    [Fact]
    public async Task ExecuteAsync_WhitespaceArtifactName_ShouldFailAtArtifactConstruction()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var executor = new StubCapabilityExecutor(
            "   ",
            ArtifactType.ConceptImage);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: executor);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput));

        Assert.True(
            executor.ExecuteAsyncWasCalled,
            "Executor must have been reached before Artifact construction.");
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "Persistence must not be called when Artifact construction fails.");
    }


    [Fact]
    public async Task ExecuteAsync_PreCancelledToken_ShouldThrowAndNotCallExecutor()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingExecutor = new StubCapabilityExecutor();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            executor: trackingExecutor,
            artifactRepository: trackingArtifactRepository);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput, cts.Token));

        Assert.False(trackingExecutor.ExecuteAsyncWasCalled, "Executor must not be called when token is pre-cancelled.");
        Assert.False(trackingArtifactRepository.TryAddAsyncWasCalled, "Persistence must not be called when token is pre-cancelled.");
    }


    [Fact]
    public async Task ExecuteAsync_CancellationAtExecutorBoundary_ShouldThrowAndNotPersist()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var trackingArtifactRepository = new TrackingArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        using var cts = new CancellationTokenSource();
        var cancellingExecutor = new CancelBeforeReturnExecutor(cts);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: trackingArtifactRepository,
            executor: cancellingExecutor);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput, cts.Token));

        Assert.True(
            cancellingExecutor.ExecuteAsyncWasCalled,
            "Executor must have been reached after execution and definition resolution succeeded.");
        Assert.False(
            trackingArtifactRepository.TryAddAsyncWasCalled,
            "Persistence must not be called when cancellation occurs at executor boundary.");
    }


    [Fact]
    public async Task ExecuteAsync_CancellationAtPersistenceBoundary_ShouldThrow()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        using var cts = new CancellationTokenSource();
        var cancellingArtifactRepository = new CancelBeforeAddArtifactRepository(cts);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: cancellingArtifactRepository);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.ExecuteAsync(execution.Id, 1, SharedInput, cts.Token));

        Assert.True(
            cancellingArtifactRepository.TryAddAsyncWasCalled,
            "TryAddAsync must have been reached after executor succeeded.");
    }


    [Fact]
    public async Task ExecuteAsync_PersistenceRejection_ShouldReturnArtifactPersistenceFailed()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var rejectingArtifactRepository = new RejectingArtifactRepository();
        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository: rejectingArtifactRepository);

        var result = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(result.IsFailure);
        var error = Assert.IsType<ArtifactPersistenceFailed>(result.Error);
        Assert.NotNull(rejectingArtifactRepository.CapturedArtifact);
        Assert.Equal(rejectingArtifactRepository.CapturedArtifact!.Id, error.ArtifactId);
    }


    [Fact]
    public async Task ExecuteAsync_Success_ShouldNotMutateExecution()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var originalStatus = execution.Status;
        var originalRevision = execution.Revision;
        var originalStartedAt = execution.StartedAt;
        var originalCompletedAt = execution.CompletedAt;

        var service = CreateService(executionRepository, definitionRepository);

        await service.ExecuteAsync(execution.Id, 1, SharedInput);

        var reloaded = await executionRepository.GetAsync(execution.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(originalStatus, reloaded!.Status);
        Assert.Equal(originalRevision, reloaded.Revision);
        Assert.Equal(originalStartedAt, reloaded.StartedAt);
        Assert.Equal(originalCompletedAt, reloaded.CompletedAt);
    }


    [Fact]
    public async Task ExecuteAsync_RepeatedInvocation_ShouldProduceDistinctArtifacts()
    {
        var executionRepository = new InMemoryWorkflowExecutionRepository();
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        var artifactRepository = new InMemoryArtifactRepository();
        var definitionId = WorkflowDefinitionId.New();

        await PersistDefinitionAsync(
            definitionRepository,
            definitionId,
            1,
            new WorkflowStep(1, CapabilityId.New()));

        var execution = await PersistRunningExecutionAsync(
            executionRepository,
            definitionId: definitionId,
            definitionVersion: 1);

        var service = CreateService(
            executionRepository,
            definitionRepository,
            artifactRepository);

        var first = await service.ExecuteAsync(execution.Id, 1, SharedInput);
        var second = await service.ExecuteAsync(execution.Id, 1, SharedInput);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.NotEqual(first.Value!.Artifact.Id, second.Value!.Artifact.Id);
    }


    [Fact]
    public void Constructor_NullExecutionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowStepService(
                null!,
                new InMemoryWorkflowDefinitionRepository(),
                new InMemoryArtifactRepository(),
                new StubCapabilityExecutor()));
    }


    [Fact]
    public void Constructor_NullDefinitionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowStepService(
                new InMemoryWorkflowExecutionRepository(),
                null!,
                new InMemoryArtifactRepository(),
                new StubCapabilityExecutor()));
    }


    [Fact]
    public void Constructor_NullArtifactRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowStepService(
                new InMemoryWorkflowExecutionRepository(),
                new InMemoryWorkflowDefinitionRepository(),
                null!,
                new StubCapabilityExecutor()));
    }


    [Fact]
    public void Constructor_NullExecutor_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new ExecuteWorkflowStepService(
                new InMemoryWorkflowExecutionRepository(),
                new InMemoryWorkflowDefinitionRepository(),
                new InMemoryArtifactRepository(),
                null!));
    }


    private sealed class StubCapabilityExecutor : ICapabilityExecutor
    {
        private readonly CapabilityExecutionOutput _output;

        public CapabilityExecutionRequest? CapturedRequest { get; private set; }

        public bool ExecuteAsyncWasCalled { get; private set; }

        public int CallCount { get; private set; }


        public StubCapabilityExecutor(
            string artifactName = "test-output.png",
            ArtifactType artifactType = ArtifactType.ConceptImage,
            IEnumerable<ArtifactId>? sourceArtifactIds = null,
            ArtifactContent? content = null)
        {
            _output = new CapabilityExecutionOutput(
                artifactName,
                artifactType,
                sourceArtifactIds ?? Array.Empty<ArtifactId>(),
                content ?? SharedContent);
        }


        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CapturedRequest = request;
            ExecuteAsyncWasCalled = true;
            CallCount++;

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(_output));
        }
    }


    private sealed class ThrowingCapabilityExecutor : ICapabilityExecutor
    {
        private readonly Exception _exception;

        public ThrowingCapabilityExecutor(Exception exception)
        {
            _exception = exception;
        }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            throw _exception;
        }
    }


    private sealed class CancelBeforeReturnExecutor : ICapabilityExecutor
    {
        private readonly CancellationTokenSource _cts;

        public bool ExecuteAsyncWasCalled { get; private set; }

        public CancelBeforeReturnExecutor(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteAsyncWasCalled = true;
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(new CapabilityExecutionOutput(
                    "cancelled.png",
                    ArtifactType.ConceptImage,
                    Array.Empty<ArtifactId>(),
                    SharedContent)));
        }
    }


    private sealed class NullReturningCapabilityExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CapabilityExecutionOutcome>(null!);
        }
    }


    private sealed class FailingCapabilityExecutor : ICapabilityExecutor
    {
        private readonly CapabilityExecutionFailure _failure;

        public CapabilityExecutionRequest? CapturedRequest { get; private set; }

        public int CallCount { get; private set; }


        public FailingCapabilityExecutor(CapabilityExecutionFailure failure)
        {
            _failure = failure;
        }


        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CapturedRequest = request;
            CallCount++;

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionFailed(_failure));
        }
    }


    private sealed class TrackingArtifactRepository : IArtifactRepository
    {
        public bool TryAddAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            cancellationToken.ThrowIfCancellationRequested();

            TryAddAsyncWasCalled = true;

            return Task.FromResult(true);
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Artifact?>(null);
        }
    }


    private sealed class CancelBeforeAddArtifactRepository : IArtifactRepository
    {
        private readonly CancellationTokenSource _cts;

        public bool TryAddAsyncWasCalled { get; private set; }

        public CancelBeforeAddArtifactRepository(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);

            TryAddAsyncWasCalled = true;
            _cts.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Artifact?>(null);
        }
    }


    private sealed class RejectingArtifactRepository : IArtifactRepository
    {
        public Artifact? CapturedArtifact { get; private set; }

        public Task<bool> TryAddAsync(
            Artifact artifact,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            cancellationToken.ThrowIfCancellationRequested();

            CapturedArtifact = artifact;

            return Task.FromResult(false);
        }

        public Task<Artifact?> GetAsync(
            ArtifactId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Artifact identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult<Artifact?>(null);
        }
    }


    private sealed class TrackingWorkflowExecutionRepository : IWorkflowExecutionRepository
    {
        public bool GetAsyncWasCalled { get; private set; }

        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(true);
        }

        public Task<WorkflowExecution?> GetAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken = default)
        {
            if (id.Value == Guid.Empty)
            {
                throw new ArgumentException(
                    "Workflow execution identifier cannot be empty.",
                    nameof(id));
            }

            cancellationToken.ThrowIfCancellationRequested();

            GetAsyncWasCalled = true;

            return Task.FromResult<WorkflowExecution?>(null);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(execution);
            cancellationToken.ThrowIfCancellationRequested();

            if (expectedRevision < 0)
            {
                throw new ArgumentException(
                    "Expected revision cannot be negative.",
                    nameof(expectedRevision));
            }

            return Task.FromResult<WorkflowExecution?>(null);
        }
    }
}
