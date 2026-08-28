using Lunar.Api.Bootstrap;
using Lunar.Application.Artifacts;
using Lunar.Application.Assets;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Lunar.Infrastructure.Providers;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
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

        Assert.NotNull(services.GetRequiredService<CreateAssetService>());
        Assert.NotNull(services.GetRequiredService<CreateWorkflowExecutionService>());
        Assert.NotNull(services.GetRequiredService<StartWorkflowExecutionService>());
        Assert.NotNull(services.GetRequiredService<ExecuteWorkflowStepService>());
        Assert.NotNull(services.GetRequiredService<GetArtifactContentService>());
        Assert.NotNull(services.GetRequiredService<GenerateArtifactService>());
        Assert.NotNull(services.GetRequiredService<GenerateDefaultArtifactService>());
        Assert.NotNull(services.GetRequiredService<GenerationWorkflowTarget>());
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


    [Fact]
    public async Task CompositionRoot_BootstrapWorkflowExistsAfterStartup()
    {
        var repository = _factory.Services.GetRequiredService<IWorkflowDefinitionRepository>();

        var definition = await repository.GetAsync(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            FirstProductLoopWorkflowBootstrap.WorkflowVersion);

        Assert.NotNull(definition);
        Assert.Equal("Text to Image", definition!.Name);
        Assert.Single(definition.Steps);
        Assert.Equal(1, definition.Steps[0].Position);
        Assert.Equal(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId,
            definition.Steps[0].CapabilityId);
    }


    [Fact]
    public void CompositionRoot_GenerationTargetMatchesBootstrap()
    {
        var target = _factory.Services.GetRequiredService<GenerationWorkflowTarget>();

        Assert.Equal(
            FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
            target.WorkflowDefinitionId);
        Assert.Equal(FirstProductLoopWorkflowBootstrap.WorkflowVersion, target.Version);
        Assert.Equal(FirstProductLoopWorkflowBootstrap.StepPosition, target.StepPosition);
    }


    [Fact]
    public void CompositionRoot_CapabilityExecutorResolverIsReplaceableByTestDouble()
    {
        var resolver = _factory.Services.GetRequiredService<ICapabilityExecutorResolver>();
        Assert.IsType<CapabilityExecutorResolver>(resolver);

        var resolvedTextToImage = resolver.Resolve(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId);
        Assert.IsType<DeterministicCapabilityExecutor>(resolvedTextToImage);

        var resolvedForegroundIsolation = resolver.Resolve(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId);
        Assert.IsType<DeterministicCapabilityExecutor>(resolvedForegroundIsolation);
    }
}


/// <summary>
/// Tests the real composition-root resolver behavior (without the
/// DeterministicCapabilityExecutor test double) for foreground-isolation
/// capability availability under disabled and enabled configurations.
/// </summary>
public class CapabilityResolverCompositionTests
{
    [Fact]
    public void DisabledConfig_ForegroundIsolationCapabilityIdIsUnresolved()
    {
        using var factory = new RealResolverFactory(disabled: true);

        var resolver = factory.Services.GetRequiredService<ICapabilityExecutorResolver>();

        // Text-to-image mapping remains available even when foreground
        // isolation is disabled.
        var resolvedTextToImage = resolver.Resolve(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId);
        Assert.NotNull(resolvedTextToImage);

        // Foreground isolation is not mapped when disabled.
        var resolvedForegroundIsolation = resolver.Resolve(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId);
        Assert.Null(resolvedForegroundIsolation);
    }

    [Fact]
    public void ValidConfig_ForegroundIsolationCapabilityIdResolvesToRealExecutor()
    {
        using var factory = new RealResolverFactory(disabled: false);

        var resolver = factory.Services.GetRequiredService<ICapabilityExecutorResolver>();

        // Text-to-image mapping unchanged.
        var resolvedTextToImage = resolver.Resolve(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId);
        Assert.NotNull(resolvedTextToImage);

        // Foreground isolation is mapped to the real executor when enabled.
        var resolvedForegroundIsolation = resolver.Resolve(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId);
        Assert.IsType<CloudflareImagesForegroundIsolationExecutor>(resolvedForegroundIsolation);
    }


    private sealed class RealResolverFactory : WebApplicationFactory<Program>
    {
        private readonly bool _disabled;

        public RealResolverFactory(bool disabled)
        {
            _disabled = disabled;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Synthetic Cloudflare Workers AI config so the real resolver
            // can construct the text-to-image executor.
            builder.UseSetting("Cloudflare:BaseAddress", "https://api.cloudflare.com/");
            builder.UseSetting("Cloudflare:AccountId", "test-account");
            builder.UseSetting("Cloudflare:ApiToken", "test-token");
            builder.UseSetting("Cloudflare:RequestTimeout", "00:01:00");
            builder.UseSetting("Cloudflare:TextToImageModelId", "@cf/black-forest-labs/flux-1-schnell");
            builder.UseSetting("Cloudflare:TextToImageSteps", "4");

            if (_disabled)
            {
                builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "");
                builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "");
            }
            else
            {
                builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
                builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
            }
        }
    }
}
