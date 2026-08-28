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
using Lunar.Infrastructure.Providers;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CloudflareWorkersAiOptions>()
    .Bind(builder.Configuration.GetSection("Cloudflare"));

builder.Services
    .AddOptions<CloudflareForegroundIsolationOptions>()
    .Bind(builder.Configuration.GetSection("CloudflareForegroundIsolation"))
    .Validate(
        CloudflareForegroundIsolationConfiguration.IsAcceptable,
        CloudflareForegroundIsolationConfiguration.GetValidationMessage())
    .ValidateOnStart();

builder.Services
    .AddOptions<LocalFileArtifactContentStoreOptions>()
    .Bind(builder.Configuration.GetSection("ArtifactContentStorage"))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.LocalRootPath),
        "ArtifactContentStorage:LocalRootPath must be configured.")
    .ValidateOnStart();

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions =
        ActivityTrackingOptions.TraceId
        | ActivityTrackingOptions.SpanId;
});

builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
});

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

builder.Services.AddHttpClient("CloudflareForegroundIsolation", client =>
{
    client.Timeout = Timeout.InfiniteTimeSpan;
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AllowAutoRedirect = false
});

builder.Services.AddTransient<CloudflareForegroundIsolationClient>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var options = sp.GetRequiredService<IOptions<CloudflareForegroundIsolationOptions>>().Value;

    // From() throws if disabled; the client is only resolved when the
    // composition root has determined the capability is enabled.
    var configuration = CloudflareForegroundIsolationConfiguration.From(options);

    var httpClient = httpClientFactory.CreateClient("CloudflareForegroundIsolation");
    httpClient.BaseAddress = configuration.Endpoint;

    return new CloudflareForegroundIsolationClient(httpClient, configuration);
});

builder.Services.AddTransient<CloudflareWorkersAiTextToImageExecutor>();
builder.Services.AddTransient<CloudflareImagesForegroundIsolationExecutor>();

builder.Services.AddSingleton<ICapabilityExecutorResolver>(sp =>
{
    var textToImage = sp.GetRequiredService<CloudflareWorkersAiTextToImageExecutor>();

    var foregroundOptions = sp.GetRequiredService<IOptions<CloudflareForegroundIsolationOptions>>().Value;

    var mappings = new List<KeyValuePair<CapabilityId, ICapabilityExecutor>>
    {
        KeyValuePair.Create(
            FirstProductLoopWorkflowBootstrap.TextToImageCapabilityId,
            (ICapabilityExecutor)textToImage)
    };

    // The foreground-isolation executor is only mapped when the
    // capability is enabled with valid configuration. When disabled,
    // the foreground-isolation CapabilityId remains unresolved and
    // Remove Background fails through CapabilityExecutorNotFound.
    if (CloudflareForegroundIsolationConfiguration.IsValid(foregroundOptions))
    {
        var foregroundIsolation = sp.GetRequiredService<CloudflareImagesForegroundIsolationExecutor>();
        mappings.Add(KeyValuePair.Create(
            ForegroundIsolationWorkflowBootstrap.ForegroundIsolationCapabilityId,
            (ICapabilityExecutor)foregroundIsolation));
    }

    return CapabilityExecutorResolver.Create(mappings);
});

builder.Services.AddSingleton<IAssetRepository, InMemoryAssetRepository>();
builder.Services.AddSingleton<IWorkflowDefinitionRepository, InMemoryWorkflowDefinitionRepository>();
builder.Services.AddSingleton<IWorkflowExecutionRepository, InMemoryWorkflowExecutionRepository>();
builder.Services.AddSingleton<IArtifactRepository, InMemoryArtifactRepository>();
builder.Services.AddSingleton<IGenerationInputRecordRepository, InMemoryGenerationInputRecordRepository>();

builder.Services.AddSingleton<IArtifactContentStore>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LocalFileArtifactContentStoreOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<LocalFileArtifactContentStore>>();

    var rootPath = Path.IsPathRooted(options.LocalRootPath)
        ? options.LocalRootPath
        : Path.Combine(builder.Environment.ContentRootPath, options.LocalRootPath);

    return new LocalFileArtifactContentStore(rootPath, logger);
});

builder.Services.AddTransient<CreateAssetService>();
builder.Services.AddTransient<ListAssetArtifactsService>();
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

builder.Services.AddSingleton(new ForegroundIsolationWorkflowTarget(
    ForegroundIsolationWorkflowBootstrap.ForegroundIsolationWorkflowDefinitionId,
    ForegroundIsolationWorkflowBootstrap.WorkflowVersion,
    ForegroundIsolationWorkflowBootstrap.StepPosition));

builder.Services.AddTransient<RemoveArtifactBackgroundService>();

var app = builder.Build();

await FirstProductLoopWorkflowBootstrap.EnsureWorkflowExistsAsync(
    app.Services.GetRequiredService<IWorkflowDefinitionRepository>());

await ForegroundIsolationWorkflowBootstrap.EnsureWorkflowExistsAsync(
    app.Services.GetRequiredService<IWorkflowDefinitionRepository>());

app.MapAssetEndpoints();
app.MapGenerationEndpoints();
app.MapArtifactEndpoints();

app.MapGet("/", () => "Hello World!");

app.Run();

public partial class Program;
