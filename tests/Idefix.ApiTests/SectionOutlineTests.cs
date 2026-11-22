using Idefix.Core.Markdown;

namespace Idefix.ApiTests;

public sealed class SectionOutlineTests
{
    private static readonly DocumentSection[] Sections =
    [
        Section("Mietrecht.pdf", "Kündigung", 2, "<!-- page 2 -->\n## Kündigung\nDie Frist beträgt drei Monate."),
        Section("Mietrecht.pdf", "Kaution", 3, "<!-- page 3 -->\n## Kaution\nHöchstens drei Monatsmieten."),
        Section("Zopf.docx", "Zutaten", 1, "<!-- page 1 -->\n# Zutaten\nMehl, Butter, Milch."),
    ];

    [Fact]
    public void OutlineListsIdsTitlesPagesSizesAndAPreview()
    {
        var lines = SectionOutline.Build(Sections).ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length);
        Assert.StartsWith("S1 | Mietrecht.pdf | p. 2 | Kündigung | ~", lines[0], StringComparison.Ordinal);
        Assert.EndsWith("| Die Frist beträgt drei Monate.", lines[0], StringComparison.Ordinal);
        Assert.StartsWith("S3 | Zopf.docx", lines[2], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{\"sections\": [\"S3\", \"S1\"]}")]
    [InlineData("Sure! ```json\n{\"sections\":[\"s1\",\"S3\",\"S3\"]}\n```")]
    public void SelectionIsParsedInDocumentOrder(string reply)
    {
        var picked = SectionOutline.ParseSelection(reply, Sections);

        Assert.Equal(["Kündigung", "Zutaten"], picked!.Select(s => s.Title));
    }

    [Fact]
    public void UnknownIdsAreIgnoredAndAnEmptyListMeansNothingIsRelevant()
    {
        Assert.Empty(SectionOutline.ParseSelection("{\"sections\": [\"S99\"]}", Sections)!);
        Assert.Empty(SectionOutline.ParseSelection("{\"sections\": []}", Sections)!);
    }

    [Fact]
    public void ARepliesWithoutJsonCannotBeParsed()
    {
        Assert.Null(SectionOutline.ParseSelection("I think the first section.", Sections));
    }

    private static DocumentSection Section(string file, string title, int page, string markdown) =>
        new($"Recht/{file}", "Recht", file, 1, title, page, page, markdown);
}
