using Lunar.Api.Bootstrap;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Api;

public class ForegroundIsolationWorkflowBootstrapTests
{
    [Fact]
    public async Task EnsureWorkflowExistsAsync_Missing_InsertsExpectedDefinition()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        var definition = await repository.GetAsync(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
            ForegroundIsolationWorkflowBootstrap.WorkflowVersion);

        Assert.NotNull(definition);
        Assert.Equal(ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId, definition!.Id);
        Assert.Equal(ForegroundIsolationWorkflowBootstrap.WorkflowVersion, definition.Version);
        Assert.Equal("Foreground Isolation", definition.Name);
        Assert.Single(definition.Steps);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId, definition.Steps[0].CapabilityId);
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExactExistingDefinition_IsNoOp()
    {
        var repository = new TrackingWorkflowDefinitionRepository();

        await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);
        var insertedCountAfterFirst = repository.TryAddCallCount;

        await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        Assert.Equal(insertedCountAfterFirst, repository.TryAddCallCount);
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExistingWithDifferentCapability_Throws()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var incompatible = new WorkflowDefinition(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
            ForegroundIsolationWorkflowBootstrap.WorkflowVersion,
            "Foreground Isolation",
            new[] { new WorkflowStep(1, CapabilityId.New()) });
        await repository.TryAddAsync(incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_ExistingWithDifferentName_Throws()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var incompatible = new WorkflowDefinition(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
            ForegroundIsolationWorkflowBootstrap.WorkflowVersion,
            "Different Name",
            new[] { new WorkflowStep(1, ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId) });
        await repository.TryAddAsync(incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenCompatibleReRead_Succeeds()
    {
        var expected = ForegroundIsolationWorkflowBootstrap.CreateExpectedDefinition();
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: expected);

        await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenMissingReRead_Throws()
    {
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_InsertRaceFalseThenIncompatibleReRead_Throws()
    {
        var incompatible = new WorkflowDefinition(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
            ForegroundIsolationWorkflowBootstrap.WorkflowVersion,
            "Different Name",
            new[] { new WorkflowStep(1, ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId) });
        var repository = new RaceSimulatingRepository(
            firstAddSucceeds: false,
            existingOnReRead: incompatible);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository));
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_NullRepository_ShouldThrow()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(null!));
    }

    [Fact]
    public void BuiltInIds_AreNonEmpty()
    {
        Assert.NotEqual(Guid.Empty, ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId.Value);
        Assert.NotEqual(Guid.Empty, ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId.Value);
    }

    [Fact]
    public void BuiltInIds_AreUuidV7()
    {
        Assert.Equal(7, GetUuidVersion(ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId.Value));
        Assert.Equal(7, GetUuidVersion(ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId.Value));
    }

    [Fact]
    public void BuiltInIds_AreDistinctFromTextToImage()
    {
        Assert.NotEqual(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId.Value,
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId.Value);
        Assert.NotEqual(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId.Value,
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId.Value);
    }

    [Fact]
    public async Task EnsureWorkflowExistsAsync_WorkflowName_IsProviderNeutral()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(repository);

        var definition = await repository.GetAsync(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
            ForegroundIsolationWorkflowBootstrap.WorkflowVersion);

        Assert.NotNull(definition);
        Assert.Equal("Foreground Isolation", definition!.Name);
        Assert.DoesNotContain("cloudflare", definition.Name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("birefnet", definition.Name, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("images", definition.Name, StringComparison.OrdinalIgnoreCase);
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
