using System.Net.Http.Json;
using Lunar.Api.Contracts;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Lunar.Tests.Api;

public sealed class LunarApiFactory : WebApplicationFactory<Program>
{
    private readonly string _contentRootPath;

    public DeterministicCapabilityExecutor Executor { get; } = new();

    public InMemoryAssetRepository AssetRepository { get; } = new();

    public InMemoryArtifactRepository ArtifactRepository { get; } = new();

    public InMemoryGenerationInputRecordRepository GenerationInputRecordRepository { get; } = new();

    public InMemoryWorkflowDefinitionRepository DefinitionRepository { get; } = new();

    public TrackingWorkflowExecutionRepository ExecutionRepository { get; } = new();

    public string ContentRootPath => _contentRootPath;


    public LunarApiFactory()
    {
        _contentRootPath = Path.Combine(
            System.IO.Path.GetTempPath(),
            "lunar-api-test-" + Guid.NewGuid().ToString("N"));
    }


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("SuppressStatusMessages", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ICapabilityExecutor>();
            services.AddSingleton<ICapabilityExecutor>(Executor);

            services.RemoveAll<IAssetRepository>();
            services.AddSingleton<IAssetRepository>(AssetRepository);

            services.RemoveAll<IArtifactRepository>();
            services.AddSingleton<IArtifactRepository>(ArtifactRepository);

            services.RemoveAll<IGenerationInputRecordRepository>();
            services.AddSingleton<IGenerationInputRecordRepository>(GenerationInputRecordRepository);

            services.RemoveAll<IWorkflowDefinitionRepository>();
            services.AddSingleton<IWorkflowDefinitionRepository>(DefinitionRepository);

            services.RemoveAll<IWorkflowExecutionRepository>();
            services.AddSingleton<IWorkflowExecutionRepository>(ExecutionRepository);

            services.RemoveAll<IArtifactContentStore>();
            services.AddSingleton<IArtifactContentStore>(_ =>
                new LocalFileArtifactContentStore(_contentRootPath, NullLogger<LocalFileArtifactContentStore>.Instance));
        });
    }


    public async Task<AssetId> SeedAssetAsync(string name = "Test Asset")
    {
        var assetId = AssetId.New();
        var asset = new Asset(assetId, name, AssetType.Character);
        await AssetRepository.TryAddAsync(asset);
        return assetId;
    }


    public async Task<(WorkflowDefinitionId Id, int Version)> SeedWorkflowDefinitionAsync()
    {
        var definitionId = WorkflowDefinitionId.New();
        var definition = new WorkflowDefinition(
            definitionId,
            1,
            "Test Workflow",
            new[] { new WorkflowStep(1, CapabilityId.New()) });
        await DefinitionRepository.TryAddAsync(definition);
        return (definitionId, 1);
    }


    public async Task<GenerationResponse> PostGenerationAsync(GenerationRequest request)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/generations", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GenerationResponse>())!;
    }


    public HttpClient CreateClientWithServices(Action<IServiceCollection> configureServices)
    {
        return WithWebHostBuilder(builder =>
            builder.ConfigureServices(configureServices)).CreateClient();
    }


    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            CleanupContentRoot();
        }
    }


    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        CleanupContentRoot();
    }


    private void CleanupContentRoot()
    {
        if (Directory.Exists(_contentRootPath))
        {
            Directory.Delete(_contentRootPath, recursive: true);
        }
    }
}
