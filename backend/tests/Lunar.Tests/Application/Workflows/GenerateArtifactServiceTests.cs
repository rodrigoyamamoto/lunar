using Lunar.Application;
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

public class GenerateArtifactServiceTests
{
    private static readonly AssetId SharedAssetId = AssetId.New();
    private static readonly WorkflowDefinitionId SharedDefinitionId = WorkflowDefinitionId.New();
    private static readonly CapabilityId SharedCapabilityId = CapabilityId.New();
    private static readonly TextPromptInput SharedInput = new("test prompt");
    private static readonly BinaryArtifactContent SharedContent =
        new(new byte[] { 0xFF, 0xD8 }, "image/jpeg");
    private static readonly ProducedArtifact SharedProducedArtifact =
        new ProducedArtifact(
            new Artifact(
                ArtifactId.New(),
                SharedAssetId,
                "output.jpg",
                ArtifactType.ConceptImage,
                Array.Empty<ArtifactId>(),
                WorkflowExecutionId.New()),
            SharedContent);


    [Fact]
    public void Constructor_NullWorkflowDefinitionRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateArtifactService(
                null!,
                new CreateWorkflowExecutionService(
                    new InMemoryAssetRepository(),
                    new InMemoryWorkflowDefinitionRepository(),
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<CreateWorkflowExecutionService>.Instance),
                new StartWorkflowExecutionService(
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<StartWorkflowExecutionService>.Instance),
                CreateStubExecuteService(),
                new InMemoryGenerationInputRecordRepository(),
                NullLogger<GenerateArtifactService>.Instance));
    }

    [Fact]
    public void Constructor_NullCreateWorkflowExecutionService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateArtifactService(
                new InMemoryWorkflowDefinitionRepository(),
                null!,
                new StartWorkflowExecutionService(
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<StartWorkflowExecutionService>.Instance),
                CreateStubExecuteService(),
                new InMemoryGenerationInputRecordRepository(),
                NullLogger<GenerateArtifactService>.Instance));
    }

    [Fact]
    public void Constructor_NullStartWorkflowExecutionService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateArtifactService(
                new InMemoryWorkflowDefinitionRepository(),
                new CreateWorkflowExecutionService(
                    new InMemoryAssetRepository(),
                    new InMemoryWorkflowDefinitionRepository(),
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<CreateWorkflowExecutionService>.Instance),
                null!,
                CreateStubExecuteService(),
                new InMemoryGenerationInputRecordRepository(),
                NullLogger<GenerateArtifactService>.Instance));
    }

    [Fact]
    public void Constructor_NullExecuteWorkflowStepService_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateArtifactService(
                new InMemoryWorkflowDefinitionRepository(),
                new CreateWorkflowExecutionService(
                    new InMemoryAssetRepository(),
                    new InMemoryWorkflowDefinitionRepository(),
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<CreateWorkflowExecutionService>.Instance),
                new StartWorkflowExecutionService(
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<StartWorkflowExecutionService>.Instance),
                null!,
                new InMemoryGenerationInputRecordRepository(),
                NullLogger<GenerateArtifactService>.Instance));
    }

    [Fact]
    public void Constructor_NullGenerationInputRecordRepository_ShouldThrow()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new GenerateArtifactService(
                new InMemoryWorkflowDefinitionRepository(),
                new CreateWorkflowExecutionService(
                    new InMemoryAssetRepository(),
                    new InMemoryWorkflowDefinitionRepository(),
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<CreateWorkflowExecutionService>.Instance),
                new StartWorkflowExecutionService(
                    new InMemoryWorkflowExecutionRepository(),
                    NullLogger<StartWorkflowExecutionService>.Instance),
                CreateStubExecuteService(),
                null!,
                NullLogger<GenerateArtifactService>.Instance));
    }


    [Fact]
    public async Task GenerateAsync_EmptyAssetId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync(
                new AssetId(Guid.Empty),
                SharedDefinitionId,
                1,
                1,
                SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>())));
    }

    [Fact]
    public async Task GenerateAsync_EmptyWorkflowDefinitionId_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync(
                SharedAssetId,
                new WorkflowDefinitionId(Guid.Empty),
                1,
                1,
                SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>())));
    }

    [Fact]
    public async Task GenerateAsync_VersionLessThanOne_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync(
                SharedAssetId,
                SharedDefinitionId,
                0,
                1,
                SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>())));
    }

    [Fact]
    public async Task GenerateAsync_StepPositionLessThanOne_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateAsync(
                SharedAssetId,
                SharedDefinitionId,
                1,
                0,
                SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>())));
    }

    [Fact]
    public async Task GenerateAsync_NullInput_ShouldThrow()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.GenerateAsync(
                SharedAssetId,
                SharedDefinitionId,
                1,
                1,
                null!, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>())));
    }


    [Fact]
    public async Task GenerateAsync_MissingDefinition_ShouldReturnWorkflowDefinitionNotFound()
    {
        var service = CreateService();

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowDefinitionNotFound>(result.Error);
        Assert.Equal(SharedDefinitionId, error.WorkflowDefinitionId);
        Assert.Equal(1, error.Version);
    }


    [Fact]
    public async Task GenerateAsync_MissingStep_ShouldReturnWorkflowStepNotFound()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executionRepository = new TrackingExecutionRepository();
        var service = CreateService(
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            99,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepNotFound>(result.Error);
        Assert.Equal(SharedDefinitionId, error.WorkflowDefinitionId);
        Assert.Equal(1, error.WorkflowDefinitionVersion);
        Assert.Equal(99, error.StepPosition);
    }


    [Fact]
    public async Task GenerateAsync_MissingStep_ShouldNotPersistWorkflowExecution()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executionRepository = new TrackingExecutionRepository();
        var service = CreateService(
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            99,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Equal(0, executionRepository.TryAddCallCount);
        Assert.Equal(0, executionRepository.TryUpdateCallCount);
    }


    [Fact]
    public async Task GenerateAsync_MissingStep_ShouldNotCallExecutor()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            definitionRepository: definitionRepository,
            executor: executor);

        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            99,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Equal(0, executor.CallCount);
    }


    [Fact]
    public async Task GenerateAsync_CreateFailure_ShouldPropagateExactError()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        Assert.IsType<AssetNotFound>(result.Error);
    }


    [Fact]
    public async Task GenerateAsync_StartFailure_ShouldPropagateExactError()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executionRepository = new TrackingExecutionRepository();
        executionRepository.SetRejectUpdate();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        Assert.IsType<WorkflowExecutionConcurrencyConflict>(result.Error);
    }


    [Fact]
    public async Task GenerateAsync_ExecuteFailure_ShouldPropagateExactError()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executor = new TrackingCapabilityExecutor();
        executor.Failure = new CapabilityExecutionFailure(CapabilityExecutionFailureKind.Rejected);

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        var error = Assert.IsType<WorkflowStepExecutionFailed>(result.Error);
        Assert.Equal(CapabilityExecutionFailureKind.Rejected, error.Kind);
    }


    [Fact]
    public async Task GenerateAsync_Success_ShouldReturnExactWorkflowExecutionIdAndProducedArtifact()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value!.WorkflowExecutionId.Value);
        Assert.NotNull(result.Value!.ProducedArtifact);
        Assert.NotNull(result.Value!.ProducedArtifact.Artifact);
        Assert.Equal(SharedAssetId, result.Value!.ProducedArtifact.Artifact.AssetId);
    }


    [Fact]
    public async Task GenerateAsync_Success_StartUsesNewlyCreatedExecutionId()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executionRepository = new TrackingExecutionRepository();
        var executor = new TrackingCapabilityExecutor();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);

        Assert.Equal(1, executionRepository.TryAddCallCount);
        Assert.Equal(1, executionRepository.TryUpdateCallCount);
        Assert.Equal(
            executionRepository.LastAddedExecutionId,
            executionRepository.LastUpdatedExecutionId);
        Assert.Equal(result.Value!.WorkflowExecutionId, executionRepository.LastAddedExecutionId);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExecuteUsesNewlyCreatedExecutionId()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executionRepository = new TrackingExecutionRepository();
        var executor = new TrackingCapabilityExecutor();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, executor.CallCount);
        Assert.Equal(result.Value!.WorkflowExecutionId, executor.LastRequest!.WorkflowExecutionId);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExecuteReceivesExactStepPosition()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor);

        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Equal(1, executor.LastRequest!.StepPosition);
    }


    [Fact]
    public async Task GenerateAsync_Success_ExecuteReceivesExactInputInstance()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor);

        var input = new TextPromptInput("a unique prompt");
        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            input,
            new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Same(input, executor.LastRequest!.Input);
    }


    [Fact]
    public async Task GenerateAsync_Success_ReturnsExactWorkflowExecutionIdFromCreatedExecution()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executionRepository = new TrackingExecutionRepository();
        var executor = new TrackingCapabilityExecutor();

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executionRepository: executionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);
        Assert.Equal(executionRepository.LastAddedExecutionId, result.Value!.WorkflowExecutionId);
    }


    [Fact]
    public async Task GenerateAsync_Success_ReturnsExactProducedArtifactFromExecute()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value!.ProducedArtifact);
        Assert.Equal(SharedAssetId, result.Value!.ProducedArtifact.Artifact.AssetId);
    }


    [Fact]
    public async Task GenerateAsync_PreCancelledToken_ShouldPropagate()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GenerateAsync(
                SharedAssetId,
                SharedDefinitionId,
                1,
                1,
                SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()),
                cts.Token));
    }


    [Fact]
    public async Task GenerateAsync_Success_PersistsGenerationInputRecord()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor,
            generationInputRecordRepository: inputRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsSuccess);

        var records = await inputRepository.GetByAssetIdAsync(SharedAssetId);
        Assert.Single(records);
        var record = records[0];
        Assert.Equal(result.Value!.WorkflowExecutionId, record.WorkflowExecutionId);
        Assert.Equal(SharedAssetId, record.AssetId);
        Assert.Equal(SharedInput.Prompt, record.Prompt.Prompt);
    }


    [Fact]
    public async Task GenerateAsync_Success_PreservesExactPromptWithInternalWhitespace()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor,
            generationInputRecordRepository: inputRepository);

        var promptWithWhitespace = new TextPromptInput("  a sword  with  spaces  ");
        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            promptWithWhitespace,
            new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        var records = await inputRepository.GetByAssetIdAsync(SharedAssetId);
        Assert.Single(records);
        Assert.Equal("  a sword  with  spaces  ", records[0].Prompt.Prompt);
    }


    [Fact]
    public async Task GenerateAsync_PersistenceFailure_ReturnsGenerationInputPersistenceFailed()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new RejectingGenerationInputRecordRepository();
        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor,
            generationInputRecordRepository: inputRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);
        Assert.IsType<GenerationInputPersistenceFailed>(result.Error);
    }


    [Fact]
    public async Task GenerateAsync_PersistenceFailure_DoesNotCallExecutor()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new RejectingGenerationInputRecordRepository();
        var executor = new TrackingCapabilityExecutor();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor,
            generationInputRecordRepository: inputRepository);

        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Equal(0, executor.CallCount);
    }


    [Fact]
    public async Task GenerateAsync_PrevalidationFailure_DoesNotPersistGenerationInput()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new TrackingGenerationInputRecordRepository();
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            generationInputRecordRepository: inputRepository);

        await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            99,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.Equal(0, inputRepository.TryAddCallCount);
    }


    [Fact]
    public async Task GenerateAsync_ExecuteFailure_RetainsGenerationInputRecord()
    {
        var definitionRepository = new InMemoryWorkflowDefinitionRepository();
        await definitionRepository.TryAddAsync(CreateDefinition(SharedDefinitionId, 1));

        var assetRepository = new InMemoryAssetRepository();
        await assetRepository.TryAddAsync(new Asset(SharedAssetId, "Test", AssetType.Character));

        var inputRepository = new InMemoryGenerationInputRecordRepository();
        var executor = new TrackingCapabilityExecutor();
        executor.Failure = new CapabilityExecutionFailure(CapabilityExecutionFailureKind.Rejected);
        var service = CreateService(
            assetRepository: assetRepository,
            definitionRepository: definitionRepository,
            executor: executor,
            generationInputRecordRepository: inputRepository);

        var result = await service.GenerateAsync(
            SharedAssetId,
            SharedDefinitionId,
            1,
            1,
            SharedInput, new WorkflowStepArtifactContext("test", ArtifactType.ConceptImage, Array.Empty<ArtifactId>()));

        Assert.True(result.IsFailure);

        var records = await inputRepository.GetByAssetIdAsync(SharedAssetId);
        Assert.Single(records);
        Assert.Equal(SharedInput.Prompt, records[0].Prompt.Prompt);
    }


    private static GenerateArtifactService CreateService(
        IAssetRepository? assetRepository = null,
        IWorkflowDefinitionRepository? definitionRepository = null,
        IWorkflowExecutionRepository? executionRepository = null,
        ICapabilityExecutor? executor = null,
        IArtifactRepository? artifactRepository = null,
        IArtifactContentStore? contentStore = null,
        IGenerationInputRecordRepository? generationInputRecordRepository = null)
    {
        assetRepository ??= new InMemoryAssetRepository();
        definitionRepository ??= new InMemoryWorkflowDefinitionRepository();
        executionRepository ??= new InMemoryWorkflowExecutionRepository();
        executor ??= new TrackingCapabilityExecutor();
        artifactRepository ??= new InMemoryArtifactRepository();
        contentStore ??= new InMemoryArtifactContentStore();
        generationInputRecordRepository ??= new InMemoryGenerationInputRecordRepository();

        var createService = new CreateWorkflowExecutionService(
            assetRepository, definitionRepository, executionRepository,
            NullLogger<CreateWorkflowExecutionService>.Instance);

        var startService = new StartWorkflowExecutionService(
            executionRepository,
            NullLogger<StartWorkflowExecutionService>.Instance);

        var executeService = new ExecuteWorkflowStepService(
            executionRepository, definitionRepository,
            artifactRepository, new SingleCapabilityExecutorResolver(executor), contentStore,
            NullLogger<ExecuteWorkflowStepService>.Instance);

        return new GenerateArtifactService(
            definitionRepository, createService, startService, executeService,
            generationInputRecordRepository,
            NullLogger<GenerateArtifactService>.Instance);
    }

    private static ExecuteWorkflowStepService CreateStubExecuteService()
    {
        return new ExecuteWorkflowStepService(
            new InMemoryWorkflowExecutionRepository(),
            new InMemoryWorkflowDefinitionRepository(),
            new InMemoryArtifactRepository(),
            new SingleCapabilityExecutorResolver(new TrackingCapabilityExecutor()),
            new InMemoryArtifactContentStore(),
            NullLogger<ExecuteWorkflowStepService>.Instance);
    }

    private static WorkflowDefinition CreateDefinition(WorkflowDefinitionId id, int version)
    {
        return new WorkflowDefinition(
            id,
            version,
            "Test Definition",
            new[] { new WorkflowStep(1, SharedCapabilityId) });
    }


    private sealed class TrackingExecutionRepository : IWorkflowExecutionRepository
    {
        private readonly InMemoryWorkflowExecutionRepository _inner = new();
        private bool _rejectUpdate;

        public int TryAddCallCount { get; private set; }

        public int TryUpdateCallCount { get; private set; }

        public WorkflowExecutionId LastAddedExecutionId { get; private set; }

        public WorkflowExecutionId LastUpdatedExecutionId { get; private set; }


        public void SetRejectUpdate() => _rejectUpdate = true;

        public Task<bool> TryAddAsync(
            WorkflowExecution execution,
            CancellationToken cancellationToken = default)
        {
            TryAddCallCount++;
            LastAddedExecutionId = execution.Id;
            return _inner.TryAddAsync(execution, cancellationToken);
        }

        public Task<WorkflowExecution?> GetAsync(
            WorkflowExecutionId id,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(id, cancellationToken);
        }

        public Task<WorkflowExecution?> TryUpdateAsync(
            WorkflowExecution execution,
            long expectedRevision,
            CancellationToken cancellationToken = default)
        {
            TryUpdateCallCount++;
            LastUpdatedExecutionId = execution.Id;

            if (_rejectUpdate)
            {
                return Task.FromResult<WorkflowExecution?>(null);
            }

            return _inner.TryUpdateAsync(execution, expectedRevision, cancellationToken);
        }
    }


    private sealed class TrackingCapabilityExecutor : ICapabilityExecutor
    {
        public int CallCount { get; private set; }

        public CapabilityExecutionRequest? LastRequest { get; private set; }

        public CapabilityExecutionFailure? Failure { get; set; }


        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            CallCount++;
            LastRequest = request;

            if (Failure is { } failure)
            {
                return Task.FromResult<CapabilityExecutionOutcome>(
                    new CapabilityExecutionFailed(failure));
            }

            var output = new CapabilityExecutionOutput(
            SharedContent);

            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionSucceeded(output));
        }
    }


    private sealed class InMemoryArtifactContentStore : IArtifactContentStore
    {
        private readonly Dictionary<ArtifactId, ArtifactContent> _store = new();

        public Task<bool> TryAddAsync(
            ArtifactId artifactId,
            ArtifactContent content,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(content);
            return Task.FromResult(_store.TryAdd(artifactId, content));
        }

        public Task<ArtifactContent?> GetAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            _store.TryGetValue(artifactId, out var content);
            return Task.FromResult(content);
        }

        public Task<bool> TryDeleteAsync(
            ArtifactId artifactId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_store.Remove(artifactId));
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


    private sealed class TrackingGenerationInputRecordRepository : IGenerationInputRecordRepository
    {
        private readonly InMemoryGenerationInputRecordRepository _inner = new();

        public int TryAddCallCount { get; private set; }

        public Task<bool> TryAddAsync(
            GenerationInputRecord record,
            CancellationToken cancellationToken = default)
        {
            TryAddCallCount++;
            return _inner.TryAddAsync(record, cancellationToken);
        }

        public Task<IReadOnlyList<GenerationInputRecord>> GetByAssetIdAsync(
            AssetId assetId,
            CancellationToken cancellationToken = default)
        {
            return _inner.GetByAssetIdAsync(assetId, cancellationToken);
        }
    }
}
