using Idefix.Core.Agent;
using Idefix.Core.Indexing;

namespace Idefix.Infrastructure.Indexing;

public sealed class KnowledgeSearch(ITextEmbedder embedder, IVectorIndex index) : IKnowledgeSearch
{
    public async Task<IReadOnlyList<ScoredChunk>> SearchAsync(string query, IReadOnlyCollection<string> plugins, int top, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || plugins.Count == 0)
        {
            return [];
        }

        var vectors = await embedder.EmbedAsync([query], cancellationToken);
        return await index.SearchAsync(vectors[0], plugins, top, cancellationToken);
    }
}
