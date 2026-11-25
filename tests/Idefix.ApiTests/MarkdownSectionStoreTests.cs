using Idefix.ApiTests.Support;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Markdown;
using Idefix.Infrastructure.Parsing;

namespace Idefix.ApiTests;

public sealed class MarkdownSectionStoreTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly DocumentLibrary library;
    private readonly MarkdownSectionStore store = new();
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    public MarkdownSectionStoreTests()
    {
        this.library = new DocumentLibrary(this.root.Path, new FileSystemKnowledgeCatalog(), CompositeDocumentParser.CreateDefault());
    }

    [Fact]
    public async Task SectionsAreWrittenAsMarkdownIntoThePluginsMarkdownFolder()
    {
        TestDocuments.WriteDocx(this.root.Combine("Recht", "Miete.docx"), "# Kündigung\nDie Frist beträgt drei Monate.\n# Kaution\nHöchstens drei Monatsmieten.");

        var sections = await this.SyncAsync();

        var folder = this.root.Combine("Recht", "_markdown", "Miete.docx");
        Assert.Equal(["01-kuendigung.md", "02-kaution.md"], Directory.GetFiles(folder).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        var text = await File.ReadAllTextAsync(Path.Combine(folder, "01-kuendigung.md"), this.cancellation);
        Assert.StartsWith("---\nsource: Miete.docx\nsection: 1\ntitle: Kündigung\npages: 1-1\n", text, StringComparison.Ordinal);
        Assert.Contains("# Kündigung\nDie Frist beträgt drei Monate.", text, StringComparison.Ordinal);
        Assert.Equal(["Kündigung", "Kaution"], sections.Select(s => s.Title));
    }

    [Fact]
    public async Task MarkdownFilesAreNotTreatedAsDocuments()
    {
        TestDocuments.WriteDocx(this.root.Combine("Recht", "Miete.docx"), "# Kündigung\nText");
        await this.SyncAsync();
        TestDocuments.WriteDocx(this.root.Combine("Recht", "_markdown", "Fremd.docx"), "should be ignored");

        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);

        Assert.Equal(["Recht/Miete.docx"], snapshot.Documents.Select(d => d.Document.Key));
    }

    [Fact]
    public async Task UserEditsAreKeptUntilTheOriginalChanges()
    {
        var source = TestDocuments.WriteDocx(this.root.Combine("Recht", "Miete.docx"), "# Kündigung\nOriginal");
        await this.SyncAsync();
        var file = this.root.Combine("Recht", "_markdown", "Miete.docx", "01-kuendigung.md");
        await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file, this.cancellation)).Replace("Original", "Bearbeitet", StringComparison.Ordinal), this.cancellation);

        var edited = await this.SyncAsync();
        Assert.Contains("Bearbeitet", Assert.Single(edited).Markdown, StringComparison.Ordinal);
        Assert.Equal(1, this.store.Writes);

        TestDocuments.WriteDocx(source, "# Kündigung\nNeu");
        TestDocuments.Touch(source, TimeProvider.System.GetUtcNow().AddMinutes(1));
        var regenerated = await this.SyncAsync();

        Assert.Contains("Neu", Assert.Single(regenerated).Markdown, StringComparison.Ordinal);
        Assert.Equal(2, this.store.Writes);
    }

    [Fact]
    public async Task FoldersOfDeletedDocumentsArePruned()
    {
        var gone = TestDocuments.WriteDocx(this.root.Combine("Recht", "Alt.docx"), "# Alt\nText");
        TestDocuments.WriteDocx(this.root.Combine("Recht", "Neu.docx"), "# Neu\nText");
        await this.SyncAsync();

        File.Delete(gone);
        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        await this.store.PruneAsync(snapshot.Documents.Select(d => d.Document).ToList(), this.cancellation);

        Assert.False(Directory.Exists(this.root.Combine("Recht", "_markdown", "Alt.docx")));
        Assert.True(Directory.Exists(this.root.Combine("Recht", "_markdown", "Neu.docx")));
    }

    [Fact]
    public async Task OriginalsAreNeverModified()
    {
        var source = TestDocuments.WriteDocx(this.root.Combine("Recht", "Miete.docx"), "# Kündigung\nText");
        var before = await File.ReadAllBytesAsync(source, this.cancellation);

        await this.SyncAsync();

        Assert.Equal(before, await File.ReadAllBytesAsync(source, this.cancellation));
    }

    [Fact]
    public async Task OutdatedSectionsAreRegeneratedUnlessEdited()
    {
        TestDocuments.WriteDocx(this.root.Combine("Recht", "A.docx"), "# Alt\nText");
        TestDocuments.WriteDocx(this.root.Combine("Recht", "B.docx"), "# Alt\nText");
        await this.SyncAsync();
        var untouched = this.root.Combine("Recht", "_markdown", "A.docx", "01-alt.md");
        var edited = this.root.Combine("Recht", "_markdown", "B.docx", "01-alt.md");
        await File.WriteAllTextAsync(untouched, (await File.ReadAllTextAsync(untouched, this.cancellation)).Replace("generator: 1", "generator: 0", StringComparison.Ordinal), this.cancellation);
        var editedText = (await File.ReadAllTextAsync(edited, this.cancellation)).Replace("generator: 1", "generator: 0", StringComparison.Ordinal).Replace("Text", "Meine Notiz", StringComparison.Ordinal);
        await File.WriteAllTextAsync(edited, editedText, this.cancellation);

        var sections = await this.SyncAsync();

        Assert.Contains("generator: 1", await File.ReadAllTextAsync(untouched, this.cancellation), StringComparison.Ordinal);
        Assert.Contains("Meine Notiz", sections.Single(s => s.FileName == "B.docx").Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RebuildDiscardsEditsAndRegenerates()
    {
        TestDocuments.WriteDocx(this.root.Combine("Recht", "A.docx"), "# Titel\nOriginal");
        await this.SyncAsync();
        var file = this.root.Combine("Recht", "_markdown", "A.docx", "01-titel.md");
        await File.WriteAllTextAsync(file, (await File.ReadAllTextAsync(file, this.cancellation)).Replace("Original", "Bearbeitet", StringComparison.Ordinal), this.cancellation);

        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        var rebuilt = await this.store.RebuildAsync(snapshot.Documents, this.cancellation);

        Assert.Contains("Original", Assert.Single(rebuilt).Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentSynchronizationsDoNotCollide()
    {
        for (var i = 0; i < 5; i++)
        {
            TestDocuments.WriteDocx(this.root.Combine("Recht", $"D{i}.docx"), $"# Teil {i}\nText {i}");
        }

        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => this.store.SynchronizeAsync(snapshot.Documents, this.cancellation)));

        Assert.All(runs, r => Assert.Equal(5, r.Count));
        Assert.Equal(5, this.store.Writes);
    }

    public void Dispose()
    {
        this.store.Dispose();
        this.root.Dispose();
    }

    private async Task<IReadOnlyList<Idefix.Core.Markdown.DocumentSection>> SyncAsync()
    {
        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        return await this.store.SynchronizeAsync(snapshot.Documents, this.cancellation);
    }
}
