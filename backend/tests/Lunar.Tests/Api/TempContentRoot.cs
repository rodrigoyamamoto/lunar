namespace Lunar.Tests.Api;

public sealed class TempContentRoot : IDisposable
{
    public string Path { get; }

    public TempContentRoot(string prefix)
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix + "-" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
