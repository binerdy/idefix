using Idefix.ApiTests.Support;
using Idefix.Infrastructure.Persistence;

namespace Idefix.ApiTests;

public sealed class DataDirectoryMigrationTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    private string Legacy => this.root.Combine("Idefix");

    private string Data => this.root.Combine("Idefix.Data");

    [Fact]
    public async Task ProfileChatsAndLogsMoveOutOfTheInstallFolder()
    {
        await this.WriteAsync(this.Legacy, "profile.dat", "secret");
        await this.WriteAsync(this.Legacy, "projects.json", "[]");
        await this.WriteAsync(Path.Combine(this.Legacy, "sessions"), "a.json", "{}");
        await this.WriteAsync(Path.Combine(this.Legacy, "current"), "Idefix.App.exe", "program");

        var moved = DataDirectoryMigration.Migrate(this.Legacy, this.Data);

        Assert.Equal(["profile.dat", "projects.json", "sessions"], moved);
        Assert.Equal("secret", await File.ReadAllTextAsync(Path.Combine(this.Data, "profile.dat"), this.cancellation));
        Assert.True(File.Exists(Path.Combine(this.Data, "sessions", "a.json")));
        Assert.False(File.Exists(Path.Combine(this.Legacy, "profile.dat")));
        Assert.True(File.Exists(Path.Combine(this.Legacy, "current", "Idefix.App.exe")));
    }

    [Fact]
    public async Task ExistingDataIsNeverOverwritten()
    {
        await this.WriteAsync(this.Legacy, "profile.dat", "old");
        await this.WriteAsync(this.Data, "profile.dat", "new");
        await this.WriteAsync(Path.Combine(this.Legacy, "sessions"), "a.json", "old a");
        await this.WriteAsync(Path.Combine(this.Legacy, "sessions"), "b.json", "old b");
        await this.WriteAsync(Path.Combine(this.Data, "sessions"), "a.json", "new a");

        DataDirectoryMigration.Migrate(this.Legacy, this.Data);

        Assert.Equal("new", await File.ReadAllTextAsync(Path.Combine(this.Data, "profile.dat"), this.cancellation));
        Assert.Equal("new a", await File.ReadAllTextAsync(Path.Combine(this.Data, "sessions", "a.json"), this.cancellation));
        Assert.Equal("old b", await File.ReadAllTextAsync(Path.Combine(this.Data, "sessions", "b.json"), this.cancellation));
    }

    [Fact]
    public void NothingHappensWithoutLegacyData()
    {
        Assert.Empty(DataDirectoryMigration.Migrate(this.Legacy, this.Data));
        Assert.Empty(DataDirectoryMigration.Migrate(this.Data, this.Data));
    }

    public void Dispose() => this.root.Dispose();

    private async Task WriteAsync(string directory, string name, string content)
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, name), content, this.cancellation);
    }
}
