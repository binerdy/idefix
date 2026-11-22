using Idefix.ApiTests.Support;
using Idefix.Core.Indexing;
using Idefix.Infrastructure.Indexing;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Parsing;

namespace Idefix.ApiTests;

public sealed class KnowledgeIndexerTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly HashingEmbedder embedder = new();
    private readonly SqliteVectorIndex index;
    private readonly KnowledgeIndexer indexer;
    private readonly KnowledgeSearch search;
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    public KnowledgeIndexerTests()
    {
        this.index = new SqliteVectorIndex(this.root.Path, "hashing");
        this.indexer = new KnowledgeIndexer(new FileSystemKnowledgeCatalog(), CompositeDocumentParser.CreateDefault(), this.embedder, this.index, ChunkingOptions.Default);
        this.search = new KnowledgeSearch(this.embedder, this.index);
    }

    [Fact]
    public async Task IndexedDocumentsAreSearchableAcrossPlugins()
    {
        TestDocuments.WritePdf(this.root.Combine("Recht", "Mietrecht.pdf"), "Allgemeines", "Die Kuendigungsfrist betraegt drei Monate");
        TestDocuments.WriteDocx(this.root.Combine("Kochen", "Rezepte.docx"), "Zopf braucht Butter und Mehl");

        var result = await this.indexer.SynchronizeAsync(this.root.Path, progress: null, this.cancellation);
        var hits = await this.search.SearchAsync("Kuendigungsfrist Monate", ["Recht", "Kochen"], 1, this.cancellation);

        Assert.Equal(2, result.Indexed);
        Assert.Empty(result.Failures);
        var hit = Assert.Single(hits);
        Assert.Equal(("Mietrecht.pdf", 2), (hit.Chunk.FileName, hit.Chunk.Page));
    }

    [Fact]
    public async Task ReindexingIsIncremental()
    {
        var kept = TestDocuments.WritePdf(this.root.Combine("Recht", "kept.pdf"), "unchanged content");
        var edited = TestDocuments.WriteDocx(this.root.Combine("Recht", "edited.docx"), "old content");
        var deleted = TestDocuments.WriteDocx(this.root.Combine("Recht", "deleted.docx"), "going away");
        await this.indexer.SynchronizeAsync(this.root.Path, progress: null, this.cancellation);

        TestDocuments.WriteDocx(edited, "new content");
        TestDocuments.Touch(edited, DateTime.UtcNow.AddMinutes(1));
        File.Delete(deleted);
        var second = await this.indexer.SynchronizeAsync(this.root.Path, progress: null, this.cancellation);
        var third = await this.indexer.SynchronizeAsync(this.root.Path, progress: null, this.cancellation);

        Assert.Equal((1, 1), (second.Indexed, second.Removed));
        Assert.Equal((0, 0), (third.Indexed, third.Removed));
        Assert.True(File.Exists(kept));
        var fingerprints = await this.index.GetFingerprintsAsync(this.cancellation);
        Assert.Equal(["Recht/edited.docx", "Recht/kept.pdf"], fingerprints.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task BrokenDocumentsAreReportedWithoutStoppingIndexing()
    {
        await File.WriteAllTextAsync(this.root.Combine(this.root.CreateDirectory("Recht"), "broken.pdf"), "not a pdf", this.cancellation);
        TestDocuments.WriteDocx(this.root.Combine("Recht", "ok.docx"), "fine");
        var reports = new List<IndexingProgress>();

        var result = await this.indexer.SynchronizeAsync(this.root.Path, new SynchronousProgress(reports), this.cancellation);

        Assert.Equal(1, result.Indexed);
        Assert.Equal("Recht/broken.pdf", Assert.Single(result.Failures).DocumentKey);
        Assert.True(reports[^1].IsComplete);
    }

    public void Dispose()
    {
        this.index.Dispose();
        this.root.Dispose();
    }

    private sealed class SynchronousProgress(List<IndexingProgress> reports) : IProgress<IndexingProgress>
    {
        public void Report(IndexingProgress value) => reports.Add(value);
    }
}
