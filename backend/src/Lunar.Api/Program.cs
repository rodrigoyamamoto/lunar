using Lunar.Api.Bootstrap;
using Lunar.Api.Endpoints;
using Lunar.Application.Artifacts;
using Lunar.Application.Assets;
using Lunar.Application.Workflows;
using Lunar.Core.Artifacts;
using Lunar.Core.Assets;
using Lunar.Core.Capabilities;
using Lunar.Core.Workflows;
using Lunar.Infrastructure.FileSystem;
using Lunar.Infrastructure.Persistence;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CloudflareWorkersAiOptions>()
    .Bind(builder.Configuration.GetSection("Cloudflare"));

builder.Services
    .AddOptions<LocalFileArtifactContentStoreOptions>()
    .Bind(builder.Configuration.GetSection("ArtifactContentStorage"))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.LocalRootPath),
        "ArtifactContentStorage:LocalRootPath must be configured.")
    .ValidateOnStart();

builder.Services.AddHttpClient("CloudflareWorkersAi", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false
});

builder.Services.AddTransient<CloudflareWorkersAiClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var options = sp.GetRequiredService<IOptions<CloudflareWorkersAiOptions>>().Value;

    var configuration = CloudflareWorkersAiConfiguration.From(options);

    var httpClient = httpClientFactory.CreateClient("CloudflareWorkersAi");
    httpClient.BaseAddress = configuration.BaseAddress;

    return new CloudflareWorkersAiClient(httpClient, configuration);
});

builder.Services.AddTransient<ICapabilityExecutor, CloudflareWorkersAiTextToImageExecutor>();

builder.Services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();
builder.Services.AddSingleton<IWorkflowDefinitionRepository, InMemoryWorkflowDefinitionRepository>();
builder.Services.AddSingleton<IWorkflowExecutionRepository, InMemoryWorkflowExecutionRepository>();
builder.Services.AddSingleton<IArtifactRepository, InMemoryArtifactRepository>();

builder.Services.AddSingleton<IArtifactContentStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LocalFileArtifactContentStoreOptions>>().Value;

    var rootPath = Path.IsPathRooted(options.LocalRootPath)
        ? options.LocalRootPath
        : Path.Combine(builder.Environment.ContentRootPath, options.LocalRootPath);

    return new LocalFileArtifactContentStore(rootPath);
});

builder.Services.AddTransient<CreateAssetService>();
builder.Services.AddTransient<CreateWorkflowExecutionService>();
builder.Services.AddTransient<StartWorkflowExecutionService>();
builder.Services.AddTransient<ExecuteWorkflowStepService>();
builder.Services.AddTransient<GetArtifactContentService>();
builder.Services.AddTransient<GenerateArtifactService>();

builder.Services.AddSingleton(new GenerationWorkflowTarget(
    FirstProductLoopWorkflowBootstrap.TextToImageWorkflowDefinitionId,
    FirstProductLoopWorkflowBootstrap.WorkflowVersion,
    FirstProductLoopWorkflowBootstrap.StepPosition));

builder.Services.AddTransient<GenerateDefaultArtifactService>();

var app = builder.Build();

await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(
    app.Services.GetRequiredService<IWorkflowDefinitionRepository>());

app.MapAssetEndpoints();
app.MapGenerationEndpoints();
app.MapArtifactEndpoints();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program;
