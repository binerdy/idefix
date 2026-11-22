using Idefix.Core.Knowledge;

namespace Idefix.Core.Indexing;

public interface IVectorIndex
{
    Task<IReadOnlyDictionary<string, DocumentFingerprint>> GetFingerprintsAsync(CancellationToken cancellationToken);

    Task UpsertDocumentAsync(KnowledgeDocument document, IReadOnlyList<EmbeddedChunk> chunks, CancellationToken cancellationToken);

    Task RemoveDocumentAsync(string documentKey, CancellationToken cancellationToken);

    Task<IReadOnlyList<ScoredChunk>> SearchAsync(ReadOnlyMemory<float> query, IReadOnlyCollection<string> plugins, int top, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(string documentKey, CancellationToken cancellationToken);
}
