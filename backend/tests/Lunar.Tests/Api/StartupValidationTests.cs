using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Options;

namespace Lunar.Tests.Api;

public class StartupValidationTests
{
    [Fact]
    public async Task HostStartup_WithBlankLocalRootPath_ThrowsOptionsValidationException()
    {
        var factory = new BlankContentRootFactory();

        try
        {
            var exception = await Assert.ThrowsAsync<OptionsValidationException>(() =>
            {
                factory.CreateClient();
                return Task.CompletedTask;
            });

            Assert.Contains("LocalRootPath", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithMissingForegroundIsolationEndpoint_ThrowsOptionsValidationException()
    {
        var factory = new MissingEndpointFactory();

        try
        {
            await Assert.ThrowsAsync<OptionsValidationException>(() =>
            {
                factory.CreateClient();
                return Task.CompletedTask;
            });
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithMissingForegroundIsolationServiceToken_ThrowsOptionsValidationException()
    {
        var factory = new MissingServiceTokenFactory();

        try
        {
            await Assert.ThrowsAsync<OptionsValidationException>(() =>
            {
                factory.CreateClient();
                return Task.CompletedTask;
            });
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithHttpForegroundIsolationEndpoint_ThrowsOptionsValidationException()
    {
        var factory = new HttpEndpointFactory();

        try
        {
            await Assert.ThrowsAsync<OptionsValidationException>(() =>
            {
                factory.CreateClient();
                return Task.CompletedTask;
            });
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithNonPositiveForegroundIsolationTimeout_ThrowsOptionsValidationException()
    {
        var factory = new NonPositiveTimeoutFactory();

        try
        {
            await Assert.ThrowsAsync<OptionsValidationException>(() =>
            {
                factory.CreateClient();
                return Task.CompletedTask;
            });
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithValidForegroundIsolationConfig_StartsSuccessfully()
    {
        var factory = new ValidConfigFactory();

        try
        {
            // Should not throw — valid configuration passes ValidateOnStart
            var client = factory.CreateClient();
            Assert.NotNull(client);
        }
        finally
        {
            factory.Dispose();
        }
    }


    private sealed class BlankContentRootFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ArtifactContentStorage:LocalRootPath", "");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
        }
    }

    private sealed class MissingEndpointFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
        }
    }

    private sealed class MissingServiceTokenFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "");
        }
    }

    private sealed class HttpEndpointFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "http://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
        }
    }

    private sealed class NonPositiveTimeoutFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
            builder.UseSetting("CloudflareForegroundIsolation:RequestTimeout", "00:00:00");
        }
    }

    private sealed class ValidConfigFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
            builder.UseSetting("CloudflareForegroundIsolation:RequestTimeout", "00:02:00");
        }
    }
}
