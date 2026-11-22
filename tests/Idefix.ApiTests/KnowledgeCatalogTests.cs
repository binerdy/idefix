using Idefix.ApiTests.Support;
using Idefix.Core.Knowledge;
using Idefix.Infrastructure.Knowledge;

namespace Idefix.ApiTests;

public sealed class KnowledgeCatalogTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly FileSystemKnowledgeCatalog catalog = new();

    [Fact]
    public void EveryImmediateSubdirectoryIsAPlugin()
    {
        this.root.CreateDirectory("Recht");
        this.root.CreateDirectory("Finanzen", "Nested");
        this.root.CreateDirectory(KnowledgeLayout.IndexFolderName);
        this.root.CreateDirectory(".git");

        var plugins = this.catalog.DiscoverPlugins(this.root.Path);

        Assert.Equal(["Finanzen", "Recht"], plugins.Select(p => p.Name));
    }

    [Fact]
    public void MissingRootHasNoPlugins()
    {
        Assert.Empty(this.catalog.DiscoverPlugins(this.root.Combine("does-not-exist")));
    }

    [Fact]
    public void DocumentsAreFoundRecursivelyAndOnlyPdfAndDocx()
    {
        var plugin = new KnowledgePlugin("Recht", this.root.CreateDirectory("Recht"));
        TestDocuments.WritePdf(this.root.Combine("Recht", "a.pdf"), "Alpha");
        TestDocuments.WriteDocx(this.root.Combine("Recht", "sub", "b.docx"), "Beta");
        File.WriteAllText(this.root.Combine("Recht", "notes.txt"), "ignored");
        File.WriteAllText(this.root.Combine("Recht", "~$b.docx"), "office lock file");

        var documents = this.catalog.ScanDocuments(plugin);

        Assert.Equal(["Recht/a.pdf", "Recht/sub/b.docx"], documents.Select(d => d.Key).Order(StringComparer.Ordinal));
    }

    public void Dispose() => this.root.Dispose();
}
