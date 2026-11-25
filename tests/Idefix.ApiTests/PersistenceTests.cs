using System.Runtime.Versioning;
using System.Text;
using Idefix.ApiTests.Support;
using Idefix.Core.Chat;
using Idefix.Core.Documents;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Persistence;

namespace Idefix.ApiTests;

public sealed class PersistenceTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task ProfileIsEncryptedWithDpapiAndRoundTrips()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows-only.");
        var path = this.root.Combine("profile.dat");
        var store = new DpapiSettingsStore(path);
        var settings = IdefixSettings.CreateDefault(this.root.Path, "Anna") with
        {
            Llm = new LlmSettings(LlmProviderKind.Ollama, new Uri("http://localhost:11434/v1/", UriKind.Absolute), "secret-key-123", "llama3.3", MaxContextTokens: 524_288),
            NightlyUpdates = true,
            DisabledPlugins = ["Medizin"],
        };

        await store.SaveAsync(settings, this.cancellation);
        var loaded = await store.LoadAsync(this.cancellation);

        Assert.DoesNotContain("secret-key-123", Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path, this.cancellation)), StringComparison.Ordinal);
        Assert.NotNull(loaded);
        Assert.Equal(settings.Llm, loaded.Llm);
        Assert.Equal(["Medizin"], loaded.DisabledPlugins);
        Assert.True(loaded.NightlyUpdates);
    }

    [Fact]
    [SupportedOSPlatform("windows")]
    public async Task MissingProfileLoadsAsNull()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "DPAPI is Windows-only.");
        Assert.Null(await new DpapiSettingsStore(this.root.Combine("none.dat")).LoadAsync(this.cancellation));
    }

    [Fact]
    public async Task ProjectsAndSessionsRoundTrip()
    {
        var store = new JsonSessionStore(this.root.Path);
        var project = new Project(Guid.NewGuid(), "Project A", ["Recht"]);
        var session = ChatSession.Start(project.Id, TimeProvider.System)
            .Append(ChatMessage.FromUser("Wie lange ist die Kündigungsfrist?", TimeProvider.System))
            .Append(new ChatMessage(
                ChatRole.Assistant,
                "Drei Monate [Mietrecht.pdf, p. 2].",
                TimeProvider.System.GetUtcNow(),
                new ContextSummary(["Recht/Mietrecht.pdf"], 1234),
                [new Citation("Mietrecht.pdf", 2, "Recht/Mietrecht.pdf", 1, 3)]));

        await store.SaveProjectAsync(project, this.cancellation);
        await store.SaveSessionAsync(session, this.cancellation);

        var loadedProject = Assert.Single(await store.LoadProjectsAsync(this.cancellation));
        var loadedSession = Assert.Single(await store.LoadSessionsAsync(this.cancellation));
        Assert.Equal(project.Name, loadedProject.Name);
        Assert.Equal(["Recht"], loadedProject.PluginScope!);
        Assert.Equal("Wie lange ist die Kündigungsfrist?", loadedSession.Title);
        Assert.Equal(session.Messages[1].Citations, loadedSession.Messages[1].Citations);
        Assert.Equal(session.Messages[1].Context!.Documents, loadedSession.Messages[1].Context!.Documents);
        Assert.Equal(1234, loadedSession.Messages[1].Context!.EstimatedTokens);
    }

    [Fact]
    public async Task DeletingAProjectKeepsItsSessions()
    {
        var store = new JsonSessionStore(this.root.Path);
        var project = new Project(Guid.NewGuid(), "Project B");
        await store.SaveProjectAsync(project, this.cancellation);
        await store.SaveSessionAsync(ChatSession.Start(project.Id, TimeProvider.System), this.cancellation);

        await store.DeleteProjectAsync(project.Id, this.cancellation);

        Assert.Empty(await store.LoadProjectsAsync(this.cancellation));
        Assert.Null(Assert.Single(await store.LoadSessionsAsync(this.cancellation)).ProjectId);
    }

    public void Dispose() => this.root.Dispose();
}
