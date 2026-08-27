using Lunar.Api.Bootstrap;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Api;

public class FirstProductLoopWorkflowBootstrapTests
{
    [Fact]
    public async Task EnsureWorkflowExistsAsync_Missing_InsertsExpectedDefinition()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        var definition = await repository.GetAsync(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion);

        Assert.NotNull(definition);
        Assert.Equal(FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId, definition!.Id);
        Assert.Equal(FirstProductLoopWorkflowBootstrap.WorkflowVersion, definition.Version);
        Assert.Equal("Text to Image", definition.Name);
        Assert.Single(definition.Steps);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId, definition.Steps[0].CapabilityId);
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExactExistingDefinition_IsNoOp()
    {
        var repository = new TrackingWorkflowDefinitionRepository();

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);
        var insertedCountAfterFirst = repository.TryAddCallCount;

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        Assert.Equal(insertedCountAfterFirst, repository.TryAddCallCount);
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExistingWithDifferentCapability_Throws()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var incompatible = new WorkflowDefinition(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion,
            "Text to Image",
            new[] { new WorkflowStep(1, CapabilityId.New()) });
        await repository.TryAddAsync(incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExistingWithDifferentStepShape_Throws()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var incompatible = new WorkflowDefinition(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion,
            "Text to Image",
            new[]
            {
                new WorkflowStep(1, FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId),
                new WorkflowStep(2, CapabilityId.New())
            });
        await repository.TryAddAsync(incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExistingWithDifferentName_Throws()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var incompatible = new WorkflowDefinition(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion,
            "Different Name",
            new[] { new WorkflowStep(1, FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId) });
        await repository.TryAddAsync(incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenCompatibleReRead_Succeeds()
    {
        var expected = FirstProductLoopWorkflowBootstrap.CreateExpectedDefinition();
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: expected);

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenMissingReRead_Throws()
    {
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenIncompatibleReRead_Throws()
    {
        var incompatible = new WorkflowDefinition(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion,
            "Different Name",
            new[] { new WorkflowStep(1, FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId) });
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_NullRepository_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(null!));
    }


    [Fact]
    public void BuiltInIds_AreNonEmpty()
    {
        Assert.NotEqual(Guid.Empty, FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId.Value);
        Assert.NotEqual(Guid.Empty, FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId.Value);
    }


    [Fact]
    public void BuiltInIds_AreUuidV7()
    {
        Assert.Equal(7, GetUuidVersion(FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId.Value));
        Assert.Equal(7, GetUuidVersion(FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId.Value));
    }

    [Fact]
    public void BuiltInIds_AreDistinct()
    {
        Assert.NotEqual(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId.Value,
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId.Value);
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_RunningTwice_SecondCallDoesNotAttemptInsert()
    {
        var repository = new TrackingWorkflowDefinitionRepository();

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);
        var callsAfterFirst = repository.TryAddCallCount;

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        Assert.Equal(callsAfterFirst, repository.TryAddCallCount);
    }


    [Fact]
    public async Task EnsureWorkflowExistsAsync_WorkflowName_IsProviderNeutral()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        var definition = await repository.GetAsync(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion);

        Assert.NotNull(definition);
        Assert.Equal("Text to Image", definition!.Name);
        Assert.DoesNotContain("cloudflare", definition.Name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("flux", definition.Name, StringComparison.OrdinalIgnoreCase);
    }


    private static int GetUuidVersion(Guid guid)
    {
        var versionChar = guid.ToString()[14];
        return versionChar - '0';
    }


    private sealed class TrackingWorkflowDefinitionRepository : IWorkflowDefinitionRepository
    {
        private readonly InMemoryWorkflowDefinitionRepository _inner = new();
        public int TryAddCallCount { get; private set; }

        public Task<bool> TryAddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            TryAddCallCount++;
            return _inner.TryAddAsync(definition, cancellationToken);
        }

        public Task<WorkflowDefinition?> GetAsync(WorkflowDefinitionId id, int version, CancellationToken cancellationToken = default)
        {
            return _inner.GetAsync(id, version, cancellationToken);
        }
    }


    private sealed class RaceSimulatingRepository : IWorkflowDefinitionRepository
    {
        private readonly bool _firstAddSucceeds;
        private readonly WorkflowDefinition? _existingOnReRead;
        private int _addCalls;

        public RaceSimulatingRepository(bool firstAddSucceeds, WorkflowDefinition? existingOnReRead)
        {
            _firstAddSucceeds = firstAddSucceeds;
            _existingOnReRead = existingOnReRead;
        }

        public Task<bool> TryAddAsync(WorkflowDefinition definition, CancellationToken cancellationToken = default)
        {
            _addCalls++;
            return Task.FromResult(_firstAddSucceeds);
        }

        public Task<WorkflowDefinition?> GetAsync(WorkflowDefinitionId id, int version, CancellationToken cancellationToken = default)
        {
            if (_addCalls > 0)
            {
                return Task.FromResult(_existingOnReRead);
            }

            return Task.FromResult<WorkflowDefinition?>(null);
        }
    }
}
