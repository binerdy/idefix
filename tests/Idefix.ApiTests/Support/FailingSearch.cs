using Idefix.Core.Agent;
using Idefix.Core.Indexing;

namespace Idefix.ApiTests.Support;

internal sealed class FailingSearch : IKnowledgeSearch
{
    public Task<IReadOnlyList<ScoredChunk>> SearchAsync(string query, IReadOnlyCollection<string> plugins, int top, CancellationToken cancellationToken) =>
        throw new HttpRequestException("model 'nomic-embed-text' not found");
}
