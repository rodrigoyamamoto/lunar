using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.Persistence;

namespace Lunar.Tests.Infrastructure.Persistence;

public class InMemoryWorkflowDefinitionRepositoryTests
{
    private static WorkflowDefinition CreateDefinition(
        WorkflowDefinitionId id,
        int version,
        string name = "Character Generation",
        int stepCount = 1)
    {
        var steps = new List<WorkflowStep>();

        for (var i = 1; i <= stepCount; i++)
        {
            steps.Add(new WorkflowStep(i, CapabilityId.New()));
        }

        return new WorkflowDefinition(id, version, name, steps);
    }


    [Fact]
    public async Task TryAddAsync_FirstInsertion_ShouldSucceed()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var definition = CreateDefinition(WorkflowDefinitionId.New(), 1);

        var result = await repository.TryAddAsync(definition);

        Assert.True(result);
    }


    [Fact]
    public async Task TryAddAsync_AddedDefinition_CanBeRetrieved()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();
        var capability = CapabilityId.New();
        var definition = new WorkflowDefinition(
            id,
            1,
            "Character Generation",
            new[]
            {
                new WorkflowStep(1, capability),
                new WorkflowStep(2, CapabilityId.New())
            });

        await repository.TryAddAsync(definition);

        var retrieved = await repository.GetAsync(id, 1);

        Assert.NotNull(retrieved);
        Assert.Equal(id, retrieved!.Id);
        Assert.Equal(1, retrieved.Version);
        Assert.Equal("Character Generation", retrieved.Name);
        Assert.Equal(2, retrieved.Steps.Count);
        Assert.Equal(1, retrieved.Steps[0].Position);
        Assert.Equal(capability, retrieved.Steps[0].CapabilityId);
        Assert.Equal(2, retrieved.Steps[1].Position);
    }


    [Fact]
    public async Task GetAsync_MissingLogicalId_ShouldReturnNull()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        var result = await repository.GetAsync(WorkflowDefinitionId.New(), 1);

        Assert.Null(result);
    }


    [Fact]
    public async Task GetAsync_MissingVersion_ShouldReturnNull()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();

        await repository.TryAddAsync(CreateDefinition(id, 1));

        var result = await repository.GetAsync(id, 2);

        Assert.Null(result);
    }


    [Fact]
    public async Task SameDefinitionId_MultipleVersions_Coexist()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();

        await repository.TryAddAsync(CreateDefinition(id, 1, "Version One"));
        await repository.TryAddAsync(CreateDefinition(id, 2, "Version Two"));

        var v1 = await repository.GetAsync(id, 1);
        var v2 = await repository.GetAsync(id, 2);

        Assert.NotNull(v1);
        Assert.NotNull(v2);
        Assert.Equal(1, v1!.Version);
        Assert.Equal("Version One", v1.Name);
        Assert.Equal(2, v2!.Version);
        Assert.Equal("Version Two", v2.Name);
    }


    [Fact]
    public async Task SameVersion_DifferentLogicalIds_Coexist()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var idA = WorkflowDefinitionId.New();
        var idB = WorkflowDefinitionId.New();

        await repository.TryAddAsync(CreateDefinition(idA, 2, "Definition A"));
        await repository.TryAddAsync(CreateDefinition(idB, 2, "Definition B"));

        var a = await repository.GetAsync(idA, 2);
        var b = await repository.GetAsync(idB, 2);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(idA, a!.Id);
        Assert.Equal("Definition A", a.Name);
        Assert.Equal(idB, b!.Id);
        Assert.Equal("Definition B", b.Name);
    }


    [Fact]
    public async Task TryAddAsync_DuplicateExactIdentity_ShouldReturnFalse()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();

        var first = await repository.TryAddAsync(CreateDefinition(id, 2));
        var second = await repository.TryAddAsync(CreateDefinition(id, 2, "Different"));

        Assert.True(first);
        Assert.False(second);
    }


    [Fact]
    public async Task TryAddAsync_Duplicate_ShouldNotOverwriteHistoricalState()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();
        var originalCapability = CapabilityId.New();

        var original = new WorkflowDefinition(
            id,
            2,
            "Original",
            new[] { new WorkflowStep(1, originalCapability) });

        var replacement = new WorkflowDefinition(
            id,
            2,
            "Replacement",
            new[] { new WorkflowStep(1, CapabilityId.New()) });

        await repository.TryAddAsync(original);
        await repository.TryAddAsync(replacement);

        var retrieved = await repository.GetAsync(id, 2);

        Assert.NotNull(retrieved);
        Assert.Equal("Original", retrieved!.Name);
        Assert.Single(retrieved.Steps);
        Assert.Equal(originalCapability, retrieved.Steps[0].CapabilityId);
    }


    [Fact]
    public async Task GetAsync_NonContiguousVersions_ExactLookupOnly()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();

        await repository.TryAddAsync(CreateDefinition(id, 1));
        await repository.TryAddAsync(CreateDefinition(id, 3));

        var v1 = await repository.GetAsync(id, 1);
        var v2 = await repository.GetAsync(id, 2);
        var v3 = await repository.GetAsync(id, 3);

        Assert.NotNull(v1);
        Assert.Null(v2);
        Assert.NotNull(v3);
    }


    [Fact]
    public async Task GetAsync_EmptyDefinitionId_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(new WorkflowDefinitionId(Guid.Empty), 1));
    }


    [Fact]
    public async Task GetAsync_ZeroVersion_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(WorkflowDefinitionId.New(), 0));
    }


    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task GetAsync_NegativeVersion_ShouldBeRejected(int version)
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            repository.GetAsync(WorkflowDefinitionId.New(), version));
    }


    [Fact]
    public async Task TryAddAsync_NullDefinition_ShouldBeRejected()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            repository.TryAddAsync(null!));
    }


    [Fact]
    public async Task TryAddAsync_PreCancelledToken_ShouldNotAddDefinition()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();
        var definition = CreateDefinition(id, 1);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.TryAddAsync(definition, cts.Token));

        var retrieved = await repository.GetAsync(id, 1);
        Assert.Null(retrieved);
    }


    [Fact]
    public async Task GetAsync_PreCancelledToken_ShouldCancel()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            repository.GetAsync(WorkflowDefinitionId.New(), 1, cts.Token));
    }


    [Fact]
    public async Task RetrievedDefinition_StepsRemainIntact()
    {
        var repository = new InMemoryWorkflowDefinitionRepository();
        var id = WorkflowDefinitionId.New();
        var capability1 = CapabilityId.New();
        var capability2 = CapabilityId.New();
        var capability3 = CapabilityId.New();

        var definition = new WorkflowDefinition(
            id,
            1,
            "Multi-Step Workflow",
            new[]
            {
                new WorkflowStep(1, capability1),
                new WorkflowStep(2, capability2),
                new WorkflowStep(3, capability3)
            });

        await repository.TryAddAsync(definition);

        var retrieved = await repository.GetAsync(id, 1);

        Assert.NotNull(retrieved);
        Assert.Equal(3, retrieved!.Steps.Count);
        Assert.Equal(1, retrieved.Steps[0].Position);
        Assert.Equal(capability1, retrieved.Steps[0].CapabilityId);
        Assert.Equal(2, retrieved.Steps[1].Position);
        Assert.Equal(capability2, retrieved.Steps[1].CapabilityId);
        Assert.Equal(3, retrieved.Steps[2].Position);
        Assert.Equal(capability3, retrieved.Steps[2].CapabilityId);
    }
}
