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
    public async Task HostStartup_WithDisabledForegroundIsolation_StartsSuccessfully()
    {
        var factory = new DisabledConfigFactory();

        try
        {
            // Both Endpoint and ServiceToken blank means the capability
            // is disabled. The host must start normally.
            var client = factory.CreateClient();
            Assert.NotNull(client);
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HostStartup_WithWhitespaceOnlyForegroundIsolation_StartsSuccessfully(
        string blankValue)
    {
        var factory = new WhitespaceDisabledConfigFactory(blankValue);

        try
        {
            var client = factory.CreateClient();
            Assert.NotNull(client);
        }
        finally
        {
            factory.Dispose();
        }
    }

    [Fact]
    public async Task HostStartup_WithEndpointOnly_ThrowsOptionsValidationException()
    {
        // Endpoint supplied but ServiceToken blank is partial configuration.
        var factory = new EndpointOnlyFactory();

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
    public async Task HostStartup_WithServiceTokenOnly_ThrowsOptionsValidationException()
    {
        // ServiceToken supplied but Endpoint blank is partial configuration.
        var factory = new ServiceTokenOnlyFactory();

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
    public async Task HostStartup_WithRelativeForegroundIsolationEndpoint_ThrowsOptionsValidationException()
    {
        var factory = new RelativeEndpointFactory();

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

    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:00:01")]
    public async Task HostStartup_WithNonPositiveForegroundIsolationTimeout_ThrowsOptionsValidationException(
        string timeout)
    {
        var factory = new NonPositiveTimeoutFactory(timeout);

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

    private sealed class DisabledConfigFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "");
        }
    }

    private sealed class WhitespaceDisabledConfigFactory : WebApplicationFactory<Program>
    {
        private readonly string _blankValue;

        public WhitespaceDisabledConfigFactory(string blankValue)
        {
            _blankValue = blankValue;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", _blankValue);
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", _blankValue);
        }
    }

    private sealed class EndpointOnlyFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "");
        }
    }

    private sealed class ServiceTokenOnlyFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
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

    private sealed class RelativeEndpointFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "relative/path");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
        }
    }

    private sealed class NonPositiveTimeoutFactory : WebApplicationFactory<Program>
    {
        private readonly string _timeout;

        public NonPositiveTimeoutFactory(string timeout)
        {
            _timeout = timeout;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("CloudflareForegroundIsolation:Endpoint", "https://test-worker.example.com/");
            builder.UseSetting("CloudflareForegroundIsolation:ServiceToken", "test-token");
            builder.UseSetting("CloudflareForegroundIsolation:RequestTimeout", _timeout);
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
