using Idefix.Core.Knowledge;

namespace Idefix.ApiTests;

public sealed class ReindexPlannerTests
{
    private static readonly DateTimeOffset Monday = new(2026, 11, 16, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void NewAndChangedDocumentsAreIndexedAndVanishedOnesRemoved()
    {
        var unchanged = Document("unchanged.pdf", 10, Monday);
        var changed = Document("changed.pdf", 20, Monday.AddHours(1));
        var added = Document("added.docx", 30, Monday);
        var indexed = new Dictionary<string, DocumentFingerprint>
        {
            [unchanged.Key] = unchanged.Fingerprint,
            [changed.Key] = new DocumentFingerprint(20, Monday),
            ["Plugin/deleted.pdf"] = new DocumentFingerprint(5, Monday),
        };

        var plan = ReindexPlanner.Plan([unchanged, changed, added], indexed);

        Assert.Equal(["Plugin/added.docx", "Plugin/changed.pdf"], plan.ToIndex.Select(d => d.Key));
        Assert.Equal(["Plugin/deleted.pdf"], plan.ToRemove);
    }

    [Fact]
    public void UpToDateIndexNeedsNoWork()
    {
        var document = Document("a.pdf", 1, Monday);

        var plan = ReindexPlanner.Plan([document], new Dictionary<string, DocumentFingerprint> { [document.Key] = document.Fingerprint });

        Assert.True(plan.IsEmpty);
    }

    private static KnowledgeDocument Document(string name, long length, DateTimeOffset lastWrite) =>
        new("Plugin", name, Path.Combine("root", "Plugin", name), length, lastWrite);
}
