using Idefix.Core.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Idefix.Infrastructure.Parsing;

public sealed class PdfDocumentParser : IDocumentParser
{
    private const double HeadingRatio = 1.2;
    private const double TopHeadingRatio = 1.6;
    private const int MaxHeadingLength = 120;

    public bool CanParse(string path) => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedDocument Parse(string path)
    {
        using var document = PdfDocument.Open(path);
        var pages = document.GetPages().Select(ReadPage).ToList();
        var bodySize = BodySize(pages.SelectMany(p => p.Lines));
        return new ParsedDocument(pages.Select(p => p.ToPage(bodySize)).ToList());
    }

    private static RawPage ReadPage(Page page)
    {
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        if (words.Count == 0)
        {
            return new RawPage(page.Number, []);
        }

        var lines = DocstrumBoundingBoxes.Instance.GetBlocks(words)
            .SelectMany(b => b.TextLines)
            .Select(line => new RawLine(
                line.Text,
                Normalize(line.BoundingBox, page),
                line.Words.SelectMany(w => w.Letters).Select(l => l.PointSize).DefaultIfEmpty(0).Average()))
            .ToList();
        return new RawPage(page.Number, lines);
    }

    private static double BodySize(IEnumerable<RawLine> lines)
    {
        var sizes = lines
            .Where(l => l.PointSize > 0)
            .SelectMany(l => Enumerable.Repeat(l.PointSize, Math.Max(1, l.Text.Length)))
            .Order()
            .ToList();
        return sizes.Count == 0 ? 0 : sizes[sizes.Count / 2];
    }

    private static TextBounds Normalize(PdfRectangle box, Page page)
    {
        var width = page.Width <= 0 ? 1 : page.Width;
        var height = page.Height <= 0 ? 1 : page.Height;
        return new TextBounds(
            box.Left / width,
            (height - box.Top) / height,
            box.Width / width,
            box.Height / height);
    }

    private sealed record RawLine(string Text, TextBounds Bounds, double PointSize)
    {
        public int HeadingLevel(double bodySize)
        {
            var text = this.Text.Trim();
            if (bodySize <= 0 || text.Length == 0 || text.Length > MaxHeadingLength || text[^1] is '.' or ',' or ';' or ':')
            {
                return 0;
            }

            var ratio = this.PointSize / bodySize;
            if (ratio >= TopHeadingRatio)
            {
                return 1;
            }

            return ratio >= HeadingRatio ? 2 : 0;
        }
    }

    private sealed record RawPage(int Number, IReadOnlyList<RawLine> Lines)
    {
        public DocumentPage ToPage(double bodySize) => new(
            this.Number,
            this.Lines.Select((line, index) => new TextLine(index + 1, line.Text, line.Bounds, line.HeadingLevel(bodySize))).ToList());
    }
}
