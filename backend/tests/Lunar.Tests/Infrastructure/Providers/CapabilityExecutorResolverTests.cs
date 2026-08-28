using Lunar.Core.Capabilities;
using Lunar.Infrastructure.Providers;

namespace Lunar.Tests.Infrastructure.Providers;

public class CapabilityExecutorResolverTests
{
    [Fact]
    public void Resolve_KnownCapability_ReturnsExecutor()
    {
        var capabilityId = CapabilityId.New();
        var executor = new StubExecutor();
        var resolver = new CapabilityExecutorResolver(
            new Dictionary<CapabilityId, ICapabilityExecutor>
            {
                [capabilityId] = executor
            });

        var resolved = resolver.Resolve(capabilityId);

        Assert.Same(executor, resolved);
    }

    [Fact]
    public void Resolve_UnknownCapability_ReturnsNull()
    {
        var resolver = new CapabilityExecutorResolver(
            new Dictionary<CapabilityId, ICapabilityExecutor>
            {
                [CapabilityId.New()] = new StubExecutor()
            });

        var resolved = resolver.Resolve(CapabilityId.New());

        Assert.Null(resolved);
    }

    [Fact]
    public void Resolve_DoesNotDependOnProviderOrModelNames()
    {
        var capabilityId = CapabilityId.New();
        var executor = new StubExecutor();
        var resolver = new CapabilityExecutorResolver(
            new Dictionary<CapabilityId, ICapabilityExecutor>
            {
                [capabilityId] = executor
            });

        // Resolution is purely by CapabilityId, not by string names
        Assert.Same(executor, resolver.Resolve(capabilityId));
        Assert.Null(resolver.Resolve(CapabilityId.New()));
    }

    [Fact]
    public void Constructor_NullExecutors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CapabilityExecutorResolver(null!));
    }

    [Fact]
    public void Create_DuplicateCapabilityId_Throws()
    {
        var capabilityId = CapabilityId.New();
        var executor1 = new StubExecutor();
        var executor2 = new StubExecutor();

        Assert.Throws<InvalidOperationException>(() =>
            CapabilityExecutorResolver.Create(new[]
            {
                KeyValuePair.Create(capabilityId, (ICapabilityExecutor)executor1),
                KeyValuePair.Create(capabilityId, (ICapabilityExecutor)executor2)
            }));
    }

    [Fact]
    public void Create_NullMappings_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            CapabilityExecutorResolver.Create(null!));
    }

    [Fact]
    public void Create_DoesNotSilentlyOverwriteFirstExecutor()
    {
        var capabilityId = CapabilityId.New();
        var executor1 = new StubExecutor();
        var executor2 = new StubExecutor();

        // Create should reject duplicates, not silently replace executor1
        // with executor2.
        Assert.Throws<InvalidOperationException>(() =>
            CapabilityExecutorResolver.Create(new[]
            {
                KeyValuePair.Create(capabilityId, (ICapabilityExecutor)executor1),
                KeyValuePair.Create(capabilityId, (ICapabilityExecutor)executor2)
            }));
    }


    private sealed class StubExecutor : ICapabilityExecutor
    {
        public Task<CapabilityExecutionOutcome> ExecuteAsync(
            CapabilityExecutionRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<CapabilityExecutionOutcome>(
                new CapabilityExecutionFailed(
                    new CapabilityExecutionFailure(
                        CapabilityExecutionFailureKind.Rejected)));
        }
    }
}
