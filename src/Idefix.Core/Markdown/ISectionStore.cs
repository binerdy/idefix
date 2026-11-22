using Idefix.Core.Knowledge;

namespace Idefix.Core.Markdown;

public interface ISectionStore
{
    Task<IReadOnlyList<DocumentSection>> SynchronizeAsync(IReadOnlyList<LoadedDocument> documents, CancellationToken cancellationToken);

    Task PruneAsync(IReadOnlyList<KnowledgeDocument> present, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentSection>> RebuildAsync(IReadOnlyList<LoadedDocument> documents, CancellationToken cancellationToken);
}
