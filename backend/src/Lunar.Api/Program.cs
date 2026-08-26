using Lunar.Core.Capabilities;
using Lunar.Infrastructure.Providers.Cloudflare;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddOptions<CloudflareWorkersAiOptions>()
    .Bind(builder.Configuration.GetSection("Cloudflare"));

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

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.Run();
