using System.Diagnostics.CodeAnalysis;
using Idefix.Core.Documents;
using Idefix.Core.Indexing;
using Idefix.Core.Knowledge;

namespace Idefix.Infrastructure.Indexing;

public sealed class KnowledgeIndexer(
    IKnowledgeCatalog catalog,
    IDocumentParser parser,
    ITextEmbedder embedder,
    IVectorIndex index,
    ChunkingOptions chunking) : IKnowledgeIndexer
{
    private const int EmbeddingBatchSize = 32;

    public async Task<IndexingResult> SynchronizeAsync(string rootDirectory, IProgress<IndexingProgress>? progress, CancellationToken cancellationToken)
    {
        var documents = catalog.DiscoverPlugins(rootDirectory)
            .SelectMany(catalog.ScanDocuments)
            .ToList();
        var plan = ReindexPlanner.Plan(documents, await index.GetFingerprintsAsync(cancellationToken));

        foreach (var key in plan.ToRemove)
        {
            await index.RemoveDocumentAsync(key, cancellationToken);
        }

        var failures = new List<IndexingFailure>();
        var completed = 0;
        foreach (var document in plan.ToIndex)
        {
            progress?.Report(new IndexingProgress(plan.ToIndex.Count, completed, document.Key));
            var failure = await this.TryIndexAsync(document, cancellationToken);
            if (failure is not null)
            {
                failures.Add(failure);
            }

            completed++;
        }

        progress?.Report(new IndexingProgress(plan.ToIndex.Count, completed, CurrentDocument: null));
        return new IndexingResult(plan.ToIndex.Count - failures.Count, plan.ToRemove.Count, failures);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "One unreadable document must not stop indexing of the others.")]
    private async Task<IndexingFailure?> TryIndexAsync(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        try
        {
            var parsed = await Task.Run(() => parser.Parse(document.FullPath), cancellationToken);
            var chunks = Chunker.Chunk(document, parsed, chunking);
            var embedded = new List<EmbeddedChunk>(chunks.Count);
            foreach (var batch in chunks.Chunk(EmbeddingBatchSize))
            {
                var vectors = await embedder.EmbedAsync(batch.Select(c => c.Text).ToList(), cancellationToken);
                embedded.AddRange(batch.Zip(vectors, (chunk, vector) => new EmbeddedChunk(chunk, vector)));
            }

            await index.UpsertDocumentAsync(document, embedded, cancellationToken);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new IndexingFailure(document.Key, ex.Message);
        }
    }
}
