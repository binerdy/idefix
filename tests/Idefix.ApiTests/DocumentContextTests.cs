using Idefix.Core.Agent;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;

namespace Idefix.ApiTests;

public sealed class DocumentContextTests
{
    [Fact]
    public void PagesAreMarkedSoTheModelCanCiteThem()
    {
        var context = DocumentContext.Build([Document("Recht", "A.pdf", "eins", "zwei")]);

        Assert.Contains("=== A.pdf (plugin Recht) ===", context.Text, StringComparison.Ordinal);
        Assert.Contains("--- [A.pdf, p. 1] ---\neins", context.Text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("--- [A.pdf, p. 2] ---\nzwei", context.Text.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void SummaryListsDocumentsAndEstimatesTokens()
    {
        var context = DocumentContext.Build([Document("Recht", "A.pdf", new string('x', 4000)), Document("Kochen", "B.docx", "kurz")]);

        Assert.Equal(["Recht/A.pdf", "Kochen/B.docx"], context.Summary.Documents);
        Assert.InRange(context.Summary.EstimatedTokens, 1000, 1100);
    }

    [Fact]
    public void EmptyPagesAreSkipped()
    {
        var empty = new LoadedDocument(
            new KnowledgeDocument("Recht", "Scan.pdf", "Scan.pdf", 1, DateTimeOffset.UnixEpoch),
            new ParsedDocument([new DocumentPage(1, [])]));

        Assert.DoesNotContain("p. 1", DocumentContext.Build([empty]).Text, StringComparison.Ordinal);
    }

    private static LoadedDocument Document(string plugin, string name, params string[] pages) =>
        new(
            new KnowledgeDocument(plugin, name, name, 1, DateTimeOffset.UnixEpoch),
            new ParsedDocument(pages.Select((text, i) => new DocumentPage(i + 1, [new TextLine(1, text, Bounds: null)])).ToList()));
}
