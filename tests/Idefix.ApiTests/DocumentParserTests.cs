using Idefix.ApiTests.Support;
using Idefix.Infrastructure.Parsing;

namespace Idefix.ApiTests;

public sealed class DocumentParserTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly CompositeDocumentParser parser = CompositeDocumentParser.CreateDefault();

    [Fact]
    public void PdfLinesKeepPagesAndNormalizedCoordinates()
    {
        var path = TestDocuments.WritePdf(this.root.Combine("a.pdf"), "Erste Zeile\nZweite Zeile", "Seite zwei");

        var parsed = this.parser.Parse(path);

        Assert.Equal(2, parsed.Pages.Count);
        Assert.Equal(["Erste Zeile", "Zweite Zeile"], parsed.Pages[0].Lines.Select(l => l.Text));
        Assert.Equal("Seite zwei", Assert.Single(parsed.Pages[1].Lines).Text);
        Assert.All(parsed.Pages.SelectMany(p => p.Lines), line =>
        {
            var bounds = Assert.NotNull(line.Bounds);
            Assert.InRange(bounds.Left, 0, 1);
            Assert.InRange(bounds.Top, 0, 1);
            Assert.True(bounds.Width > 0 && bounds.Height > 0);
        });
        Assert.True(parsed.Pages[0].Lines[0].Bounds!.Value.Top < parsed.Pages[0].Lines[1].Bounds!.Value.Top);
    }

    [Fact]
    public void DocxParagraphsAreLinesAndPageBreaksStartNewPages()
    {
        var path = TestDocuments.WriteDocx(this.root.Combine("b.docx"), "Absatz eins\nAbsatz zwei", "Absatz drei");

        var parsed = this.parser.Parse(path);

        Assert.Equal(2, parsed.Pages.Count);
        Assert.Contains(parsed.Pages[0].Lines, l => l.Text == "Absatz zwei");
        Assert.Equal("Absatz drei", Assert.Single(parsed.Pages[1].Lines).Text);
    }

    [Fact]
    public void ParsingNeverModifiesTheOriginalFile()
    {
        var path = TestDocuments.WriteDocx(this.root.Combine("c.docx"), "Original");
        var before = File.ReadAllBytes(path);
        var lastWrite = File.GetLastWriteTimeUtc(path);

        this.parser.Parse(path);

        Assert.Equal(before, File.ReadAllBytes(path));
        Assert.Equal(lastWrite, File.GetLastWriteTimeUtc(path));
    }

    [Fact]
    public void DocxHeadingStylesBecomeHeadingLevels()
    {
        var path = TestDocuments.WriteDocx(this.root.Combine("h.docx"), "# Titel\n## Unterkapitel\nFliesstext");

        var lines = this.parser.Parse(path).Pages[0].Lines;

        Assert.Equal([1, 2, 0], lines.Select(l => l.HeadingLevel));
    }

    [Fact]
    public void LargerPdfTextIsRecognizedAsHeading()
    {
        var path = TestDocuments.WritePdf(this.root.Combine("h.pdf"), "# Mietrecht\n## Kuendigung\nDie Frist betraegt drei Monate\nSie gilt auf die ortsueblichen Termine");

        var lines = this.parser.Parse(path).Pages[0].Lines;

        Assert.Equal([1, 2, 0, 0], lines.Select(l => l.HeadingLevel));
    }

    [Fact]
    public void UnsupportedFilesAreRejected()
    {
        Assert.False(this.parser.CanParse("notes.txt"));
        Assert.Throws<NotSupportedException>(() => this.parser.Parse("notes.txt"));
    }

    public void Dispose() => this.root.Dispose();
}
