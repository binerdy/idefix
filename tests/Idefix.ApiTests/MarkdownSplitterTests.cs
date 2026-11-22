using Idefix.Core.Documents;
using Idefix.Core.Knowledge;
using Idefix.Core.Markdown;

namespace Idefix.ApiTests;

public sealed class MarkdownSplitterTests
{
    [Fact]
    public void HeadingsStartNewSections()
    {
        var sections = MarkdownSplitter.Split(Document(
            Page(1, Heading("Mietrecht", 1), Body(40, "Allgemeines")),
            Page(2, Heading("Kündigung", 2), Body(40, "Frist"), Heading("Ausnahmen", 2), Body(40, "Vertrag"))));

        Assert.Equal(["Mietrecht", "Kündigung", "Ausnahmen"], sections.Select(s => s.Title));
        Assert.Equal([(1, 1), (2, 2), (2, 2)], sections.Select(s => (s.FirstPage, s.LastPage)));
        Assert.StartsWith("<!-- page 2 -->\n## Kündigung\n", sections[1].Markdown.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }

    [Fact]
    public void SectionsSpanPagesUntilTheNextHeading()
    {
        var sections = MarkdownSplitter.Split(Document(
            Page(1, Heading("Kapitel", 1), Body(40, "a")),
            Page(2, Body(40, "b")),
            Page(3, Heading("Anhang", 1), Body(40, "c"))));

        Assert.Equal((1, 2), (sections[0].FirstPage, sections[0].LastPage));
        Assert.Contains("<!-- page 2 -->", sections[0].Markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentsWithoutHeadingsAreSplitPerPage()
    {
        var sections = MarkdownSplitter.Split(Document(Page(1, Body(60, "eins")), Page(2, Body(60, "zwei"))));

        Assert.Equal(["Mietrecht, p. 1", "Mietrecht, p. 2"], sections.Select(s => s.Title));
    }

    [Fact]
    public void LongSectionsAreSplitAndKeepTheirTitle()
    {
        var lines = Enumerable.Range(0, 40).Select(i => Body(60, $"Zeile {i}")).Prepend(Heading("Lang", 1)).ToArray();

        var sections = MarkdownSplitter.Split(Document(Page(1, lines)));

        Assert.True(sections.Count > 1);
        Assert.All(sections, s => Assert.InRange(s.EstimatedTokens, 1, MarkdownSplitter.MaxSectionTokens + 100));
        Assert.All(sections, s => Assert.Equal("Lang", s.Title));
    }

    [Fact]
    public void TinyPagesAreMergedIntoTheirPredecessor()
    {
        var sections = MarkdownSplitter.Split(Document(Page(1, Body(60, "lang")), Page(2, new TextLine(1, "kurz", Bounds: null))));

        var section = Assert.Single(sections);
        Assert.Equal((1, 2), (section.FirstPage, section.LastPage));
    }

    [Fact]
    public void PageMarkersBecomeCitableMarkersForTheModel()
    {
        var section = MarkdownSplitter.Split(Document(Page(4, Body(60, "Inhalt")))).Single();

        Assert.StartsWith("--- [Mietrecht.pdf, p. 4] ---", MarkdownSplitter.ToContext(section), StringComparison.Ordinal);
    }

    private static TextLine Heading(string text, int level) => new(0, text, Bounds: null, level);

    private static TextLine Body(int words, string prefix) =>
        new(0, prefix + " " + string.Join(' ', Enumerable.Repeat("wort", words)), Bounds: null);

    private static DocumentPage Page(int number, params TextLine[] lines) =>
        new(number, lines.Select((l, i) => l with { Number = i + 1 }).ToList());

    private static LoadedDocument Document(params DocumentPage[] pages) =>
        new(new KnowledgeDocument("Recht", "Mietrecht.pdf", "Mietrecht.pdf", 1, DateTimeOffset.UnixEpoch), new ParsedDocument(pages));
}
