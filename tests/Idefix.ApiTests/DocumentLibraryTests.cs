using Idefix.ApiTests.Support;
using Idefix.Core.Knowledge;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Parsing;

namespace Idefix.ApiTests;

public sealed class DocumentLibraryTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly DocumentLibrary library;
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    public DocumentLibraryTests()
    {
        this.library = new DocumentLibrary(this.root.Path, new FileSystemKnowledgeCatalog(), CompositeDocumentParser.CreateDefault());
    }

    [Fact]
    public async Task LoadsEveryDocumentOfTheRequestedPlugins()
    {
        TestDocuments.WritePdf(this.root.Combine("Recht", "Mietrecht.pdf"), "Mietrecht");
        TestDocuments.WriteDocx(this.root.Combine("Recht", "sub", "Vertrag.docx"), "Vertrag");
        TestDocuments.WriteDocx(this.root.Combine("Kochen", "Zopf.docx"), "Zopf");

        var snapshot = await this.library.LoadAsync(["Recht"], progress: null, this.cancellation);

        Assert.Equal(["Recht/Mietrecht.pdf", "Recht/sub/Vertrag.docx"], snapshot.Documents.Select(d => d.Document.Key));
        Assert.Empty(snapshot.Failures);
    }

    [Fact]
    public async Task UnchangedDocumentsAreReadOnlyOnce()
    {
        TestDocuments.WritePdf(this.root.Combine("Recht", "a.pdf"), "A");
        var edited = TestDocuments.WriteDocx(this.root.Combine("Recht", "b.docx"), "alt");

        await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);
        Assert.Equal(2, this.library.Parses);

        TestDocuments.WriteDocx(edited, "neu");
        TestDocuments.Touch(edited, DateTime.UtcNow.AddMinutes(1));
        var snapshot = await this.library.LoadAsync(plugins: null, progress: null, this.cancellation);

        Assert.Equal(3, this.library.Parses);
        Assert.Equal("neu", snapshot.Documents.Single(d => d.Document.FileName == "b.docx").Content.Text);
    }

    [Fact]
    public async Task UnreadableDocumentsAreReportedAndTheRestStillLoads()
    {
        await File.WriteAllTextAsync(this.root.Combine(this.root.CreateDirectory("Recht"), "broken.pdf"), "not a pdf", this.cancellation);
        TestDocuments.WriteDocx(this.root.Combine("Recht", "ok.docx"), "fine");
        var reports = new List<LibraryProgress>();

        var snapshot = await this.library.LoadAsync(plugins: null, new SynchronousProgress(reports), this.cancellation);

        Assert.Equal("Recht/ok.docx", Assert.Single(snapshot.Documents).Document.Key);
        Assert.Equal("Recht/broken.pdf", Assert.Single(snapshot.Failures).DocumentKey);
        Assert.True(reports[^1].IsComplete);
    }

    public void Dispose() => this.root.Dispose();

    private sealed class SynchronousProgress(List<LibraryProgress> reports) : IProgress<LibraryProgress>
    {
        public void Report(LibraryProgress value) => reports.Add(value);
    }
}
