using Idefix.Core.Documents;
using Idefix.Core.Knowledge;

namespace Idefix.ApiTests;

public sealed class CitationLocatorTests
{
    private static readonly LoadedDocument Mietrecht = Document(
        "Recht",
        "Mietrecht.pdf",
        Page(1, "Mietrecht Uebersicht", "Allgemeine Bestimmungen"),
        Page(2, "Kuendigung", "Die Kuendigungsfrist fuer Wohnungen betraegt drei Monate.", "Sie gilt auf die ortsueblichen Termine.", "Ausnahmen regelt der Vertrag."));

    [Fact]
    public void FindsTheLinesThatSupportTheCitedSentence()
    {
        var citation = Assert.Single(CitationLocator.Resolve("Fuer Wohnungen gilt eine Kuendigungsfrist von drei Monaten [Mietrecht.pdf, p. 2].", [Mietrecht]));

        Assert.Equal(("Recht/Mietrecht.pdf", 2, 2, 2), (citation.DocumentKey, citation.Page, citation.FirstLine, citation.LastLine));
    }

    [Fact]
    public void ExtendsToNeighbouringSupportingLines()
    {
        var citation = Assert.Single(CitationLocator.Resolve(
            "Die Kuendigungsfrist betraegt drei Monate und gilt auf die ortsueblichen Termine [Mietrecht.pdf, p. 2].",
            [Mietrecht]));

        Assert.Equal((2, 3), (citation.FirstLine, citation.LastLine));
    }

    [Fact]
    public void OnlyTheSentenceBeforeTheCitationCounts()
    {
        var citations = CitationLocator.Resolve(
            "Es gibt allgemeine Bestimmungen [Mietrecht.pdf, p. 1]. Ausnahmen regelt der Vertrag [Mietrecht.pdf, p. 2].",
            [Mietrecht]);

        Assert.Equal([(1, 2, 2), (2, 4, 4)], citations.Select(c => (c.Page, c.FirstLine, c.LastLine)));
    }

    [Fact]
    public void UnknownDocumentsStayUnresolved()
    {
        var citation = Assert.Single(CitationLocator.Resolve("Etwas [Fremd.pdf, p. 3].", [Mietrecht]));

        Assert.Null(citation.DocumentKey);
        Assert.Equal(0, citation.FirstLine);
    }

    [Fact]
    public void RepeatedCitationsAreListedOnce()
    {
        var citations = CitationLocator.Resolve("Drei Monate [Mietrecht.pdf, p. 2]. Ortsuebliche Termine [Mietrecht.pdf, p. 2].", [Mietrecht]);

        Assert.Single(citations);
    }

    private static LoadedDocument Document(string plugin, string name, params DocumentPage[] pages) =>
        new(new KnowledgeDocument(plugin, name, name, 1, DateTimeOffset.UnixEpoch), new ParsedDocument(pages));

    private static DocumentPage Page(int number, params string[] lines) =>
        new(number, lines.Select((text, i) => new TextLine(i + 1, text, Bounds: null)).ToList());
}
