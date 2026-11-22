using Idefix.ApiTests.Support;
using Idefix.Core.Indexing;
using Idefix.Core.Knowledge;
using Idefix.Infrastructure.Indexing;

namespace Idefix.ApiTests;

public sealed class SqliteVectorIndexTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    [Fact]
    public async Task IndexLivesInHiddenFolderUnderTheRoot()
    {
        using var index = new SqliteVectorIndex(this.root.Path, "model-a");
        await index.GetFingerprintsAsync(this.cancellation);

        var folder = new DirectoryInfo(KnowledgeLayout.IndexDirectory(this.root.Path));
        Assert.True(folder.Exists);
        Assert.True(folder.Attributes.HasFlag(FileAttributes.Hidden));
        Assert.StartsWith(folder.FullName, index.DatabasePath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchRanksBySimilarityWithinEnabledPlugins()
    {
        using var index = new SqliteVectorIndex(this.root.Path, "model-a");
        await index.UpsertDocumentAsync(Document("Recht", "a.pdf"), [Embedded("Recht", "a.pdf", 1, [1, 0, 0]), Embedded("Recht", "a.pdf", 2, [0.7f, 0.7f, 0])], this.cancellation);
        await index.UpsertDocumentAsync(Document("Medizin", "b.pdf"), [Embedded("Medizin", "b.pdf", 1, [1, 0, 0])], this.cancellation);

        var results = await index.SearchAsync(new float[] { 1, 0, 0 }, ["Recht"], top: 5, this.cancellation);

        Assert.Equal([1, 2], results.Select(r => r.Chunk.Page));
        Assert.All(results, r => Assert.Equal("Recht", r.Chunk.PluginName));
        Assert.True(results[0].Score > results[1].Score);
    }

    [Fact]
    public async Task FingerprintsRoundTripAndRemovalDeletesChunks()
    {
        using var index = new SqliteVectorIndex(this.root.Path, "model-a");
        var document = Document("Recht", "a.pdf");
        await index.UpsertDocumentAsync(document, [Embedded("Recht", "a.pdf", 1, [1, 0])], this.cancellation);

        var fingerprints = await index.GetFingerprintsAsync(this.cancellation);
        Assert.Equal(document.Fingerprint, fingerprints[document.Key]);

        await index.RemoveDocumentAsync(document.Key, this.cancellation);

        Assert.Empty(await index.GetFingerprintsAsync(this.cancellation));
        Assert.Empty(await index.GetChunksAsync(document.Key, this.cancellation));
    }

    [Fact]
    public async Task SwitchingEmbeddingModelResetsTheIndex()
    {
        using (var index = new SqliteVectorIndex(this.root.Path, "model-a"))
        {
            await index.UpsertDocumentAsync(Document("Recht", "a.pdf"), [Embedded("Recht", "a.pdf", 1, [1, 0])], this.cancellation);
        }

        using var switched = new SqliteVectorIndex(this.root.Path, "model-b");

        Assert.Empty(await switched.GetFingerprintsAsync(this.cancellation));
    }

    public void Dispose() => this.root.Dispose();

    private static KnowledgeDocument Document(string plugin, string name) =>
        new(plugin, name, name, 42, new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

    private static EmbeddedChunk Embedded(string plugin, string name, int page, float[] vector)
    {
        var key = KnowledgeLayout.DocumentKey(plugin, name);
        var chunk = new DocumentChunk($"{key}#p{page}", key, plugin, name, page, 1, 3, $"text {page}");
        return new EmbeddedChunk(chunk, vector);
    }
}
