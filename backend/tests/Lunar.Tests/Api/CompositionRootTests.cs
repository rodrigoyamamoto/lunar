using Lunar.Application.Artifacts;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Lunar.Tests.Api;

public class CompositionRootTests : IClassFixture<LunarApiFactory>
{
    private readonly LunarApiFactory _factory;

    public CompositionRootTests(LunarApiFactory factory)
    {
        _factory = factory;
    }


    [Fact]
    public void CompositionRoot_ResolvesAllRequiredServices()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;

        Assert.NotNull(services.GetRequiredService<CreateWorkflowExecutionService>());
        Assert.NotNull(services.GetRequiredService<StartWorkflowExecutionService>());
        Assert.NotNull(services.GetRequiredService<ExecuteWorkflowStepService>());
        Assert.NotNull(services.GetRequiredService<GetArtifactContentService>());
        Assert.NotNull(services.GetRequiredService<GenerateArtifactService>());
    }

    [Fact]
    public void CompositionRoot_ResolvesRepositoriesAsSharedInstances()
    {
        using var scope1 = _factory.Services.CreateScope();
        using var scope2 = _factory.Services.CreateScope();

        var asset1 = scope1.ServiceProvider.GetRequiredService<IAssetRepository>();
        var asset2 = scope2.ServiceProvider.GetRequiredService<IAssetRepository>();
        Assert.Same(asset1, asset2);

        var def1 = scope1.ServiceProvider.GetRequiredService<IWorkflowDefinitionRepository>();
        var def2 = scope2.ServiceProvider.GetRequiredService<IWorkflowDefinitionRepository>();
        Assert.Same(def1, def2);

        var exec1 = scope1.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        var exec2 = scope2.ServiceProvider.GetRequiredService<IWorkflowExecutionRepository>();
        Assert.Same(exec1, exec2);

        var art1 = scope1.ServiceProvider.GetRequiredService<IArtifactRepository>();
        var art2 = scope2.ServiceProvider.GetRequiredService<IArtifactRepository>();
        Assert.Same(art1, art2);

        var content1 = scope1.ServiceProvider.GetRequiredService<IArtifactContentStore>();
        var content2 = scope2.ServiceProvider.GetRequiredService<IArtifactContentStore>();
        Assert.Same(content1, content2);
    }

    [Fact]
    public void CompositionRoot_ResolvesArtifactContentStoreConfiguration()
    {
        var options = _factory.Services.GetRequiredService<IOptions<LocalFileArtifactContentStoreOptions>>();
        Assert.NotNull(options.Value);
        Assert.False(string.IsNullOrWhiteSpace(options.Value.LocalRootPath));
    }
}
