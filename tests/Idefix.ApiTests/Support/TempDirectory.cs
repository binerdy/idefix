using System.Globalization;

namespace Idefix.ApiTests.Support;

internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        this.Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "idefix-tests", Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(this.Path);
    }

    public string Path { get; }

    public string Combine(params string[] parts) => System.IO.Path.Combine([this.Path, .. parts]);

    public string CreateDirectory(params string[] parts) => Directory.CreateDirectory(this.Combine(parts)).FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.Path, recursive: true);
        }
        catch (IOException)
        {
            // Windows may hold the SQLite file briefly after the last connection closes.
        }
    }
}
