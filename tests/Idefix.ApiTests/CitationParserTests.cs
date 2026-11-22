using Idefix.Core.Documents;

namespace Idefix.ApiTests;

public sealed class CitationParserTests
{
    [Fact]
    public void FindsCitationsInTheSpecifiedFormat()
    {
        var matches = CitationParser.Find("Die Frist beträgt 30 Tage [Mietrecht.pdf, p. 4] und [Vertrag 2024.docx, S. 12].");

        Assert.Equal(
            [new Citation("Mietrecht.pdf", 4), new Citation("Vertrag 2024.docx", 12)],
            matches.Select(m => m.Citation));
    }

    [Fact]
    public void CitationRendersInTheSpecifiedFormat()
    {
        Assert.Equal("[Document.pdf, p. 4]", new Citation("Document.pdf", 4).ToString());
    }

    [Theory]
    [InlineData("[notes.txt, p. 1]")]
    [InlineData("[Document.pdf]")]
    [InlineData("Document.pdf, p. 4")]
    public void IgnoresTextThatIsNotACitation(string text)
    {
        Assert.Empty(CitationParser.Find(text));
    }

    [Fact]
    public void ReportsPositionForInlineLinks()
    {
        const string text = "Siehe [A.pdf, p. 2].";

        var match = Assert.Single(CitationParser.Find(text));

        Assert.Equal("[A.pdf, p. 2]", text.Substring(match.Index, match.Length));
    }
}
