using Idefix.Core.Indexing;

namespace Idefix.Core.Agent;

public interface IKnowledgeSearch
{
    Task<IReadOnlyList<ScoredChunk>> SearchAsync(string query, IReadOnlyCollection<string> plugins, int top, CancellationToken cancellationToken);
}
