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


    private sealed class BlankContentRootFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ArtifactContentStorage:LocalRootPath", "");
        }
    }
}
