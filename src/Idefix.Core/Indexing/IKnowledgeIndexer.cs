namespace Idefix.Core.Indexing;

public interface IKnowledgeIndexer
{
    Task<IndexingResult> SynchronizeAsync(string rootDirectory, IProgress<IndexingProgress>? progress, CancellationToken cancellationToken);
}
