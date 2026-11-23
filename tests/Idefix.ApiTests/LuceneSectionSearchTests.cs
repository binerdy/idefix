using Idefix.Core.Markdown;
using Idefix.Infrastructure.Search;

namespace Idefix.ApiTests;

public sealed class LuceneSectionSearchTests : IDisposable
{
    private static readonly DocumentSection[] Sections =
    [
        Section(1, "Einleitung", "Dieses Reglement gilt für das ganze Gemeindegebiet und regelt die Nutzung."),
        Section(2, "Erforderlicher Parkraum", "Pro Einfamilienhaus müssen mindestens zwei Abstellplätze erstellt werden."),
        Section(3, "Dachaufbauten", "Dachaufbauten sind nur auf Dachflächen mit einer Mindestneigung zulässig."),
        Section(4, "Kündigung", "Die Frist beträgt drei Monate."),
        Section(5, "Erschliessung", "Wer ein Haus braucht, findet hier viele Hinweise zu Wegen."),
    ];

    private readonly LuceneSectionSearch search = new();

    [Theory]
    [InlineData("Abstellplatz", "Erforderlicher Parkraum")]
    [InlineData("Wie viele Plätze braucht ein Einfamilienhaus?", "Erforderlicher Parkraum")]
    [InlineData("Dachaufbaute", "Dachaufbauten")]
    [InlineData("Kuendigung", "Kündigung")]
    [InlineData("KÜNDIGUNGEN", "Kündigung")]
    public void GermanWordFormsFindTheSection(string query, string title)
    {
        Assert.Equal(title, this.search.Search(query, Sections, 8)[0].Title);
    }

    [Fact]
    public void LetterGroupsRankRelatedCompoundsBehindExactWords()
    {
        var found = this.search.Search("Einfamilienhaus Dachneigung", Sections, 8).Select(s => s.Title).ToList();

        Assert.Equal("Erforderlicher Parkraum", found[0]);
        Assert.Contains("Dachaufbauten", found);
    }

    [Theory]
    [InlineData("Quantenphysik")]
    [InlineData("Was ist das für eine?")]
    [InlineData("?!")]
    [InlineData("")]
    public void WithoutACommonWordNothingIsFound(string query)
    {
        Assert.Empty(this.search.Search(query, Sections, 8));
    }

    [Fact]
    public void ResultsAreLimited()
    {
        Assert.Single(this.search.Search("Reglement Einfamilienhaus Dachaufbauten Frist", Sections, 1));
    }

    [Fact]
    public void TheIndexIsReusedUntilTheSectionsChange()
    {
        this.search.Search("Frist", Sections, 8);
        this.search.Search("Reglement", [.. Sections], 8);
        Assert.Equal(1, this.search.Builds);

        var edited = Sections.Select(s => s.Title == "Einleitung" ? s with { Markdown = "Neu geregelt: Wärmepumpen." } : s).ToList();
        Assert.Equal("Einleitung", this.search.Search("Wärmepumpe", edited, 8)[0].Title);
        Assert.Equal(2, this.search.Builds);
    }

    [Fact]
    public void AnEmptyLibraryAndAPastedPageAreHandled()
    {
        Assert.Empty(this.search.Search("Frist", [], 8));
        var page = string.Join(' ', Enumerable.Range(0, 3_000).Select(i => $"wort{i}")) + " Frist";
        Assert.NotNull(this.search.Search(page, Sections, 8));
    }

    public void Dispose() => this.search.Dispose();

    private static DocumentSection Section(int index, string title, string markdown) =>
        new("Recht/Reglement.pdf", "Recht", "Reglement.pdf", index, title, index, index, $"<!-- page {index} -->\n## {title}\n{markdown}");
}
