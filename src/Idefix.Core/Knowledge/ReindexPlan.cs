namespace Idefix.Core.Knowledge;

public sealed record ReindexPlan(IReadOnlyList<KnowledgeDocument> ToIndex, IReadOnlyList<string> ToRemove)
{
    public bool IsEmpty => this.ToIndex.Count == 0 && this.ToRemove.Count == 0;
}
