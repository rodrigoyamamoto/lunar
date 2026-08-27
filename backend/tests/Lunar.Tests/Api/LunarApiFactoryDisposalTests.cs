namespace Lunar.Tests.Api;

public class LunarApiFactoryDisposalTests
{
    [Fact]
    public async Task LunarApiFactory_DisposeAsync_ShouldDisposeBaseAndRemoveTempRoot()
    {
        var factory = new LunarApiFactory();

        // Start the host and create a client so framework resources are allocated.
        var client = factory.CreateClient();
        Assert.NotNull(client);

        var root = factory.ContentRootPath;

        // Ensure the temp content root directory exists before disposal.
        // The factory creates the path string in the constructor, but the
        // LocalFileArtifactContentStore creates the directory lazily on first
        // write. Create it explicitly so we can prove disposal removes it.
        Directory.CreateDirectory(root);
        Assert.True(Directory.Exists(root));

        await factory.DisposeAsync();

        Assert.False(Directory.Exists(root));
    }
}
