namespace Idefix.Core.Knowledge;

public static class ReindexPlanner
{
    public static ReindexPlan Plan(IReadOnlyCollection<KnowledgeDocument> current, IReadOnlyDictionary<string, DocumentFingerprint> indexed)
    {
        var toIndex = current
            .Where(d => !indexed.TryGetValue(d.Key, out var fingerprint) || fingerprint != d.Fingerprint)
            .OrderBy(d => d.Key, StringComparer.Ordinal)
            .ToList();

        var currentKeys = current.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        var toRemove = indexed.Keys
            .Where(k => !currentKeys.Contains(k))
            .Order(StringComparer.Ordinal)
            .ToList();

        return new ReindexPlan(toIndex, toRemove);
    }
}
