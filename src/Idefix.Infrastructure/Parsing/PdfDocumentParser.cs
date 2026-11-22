using Idefix.Core.Documents;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace Idefix.Infrastructure.Parsing;

public sealed class PdfDocumentParser : IDocumentParser
{
    public bool CanParse(string path) => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);

    public ParsedDocument Parse(string path)
    {
        using var document = PdfDocument.Open(path);
        var pages = document.GetPages().Select(ParsePage).ToList();
        return new ParsedDocument(pages);
    }

    private static DocumentPage ParsePage(Page page)
    {
        var words = page.GetWords(NearestNeighbourWordExtractor.Instance).ToList();
        if (words.Count == 0)
        {
            return new DocumentPage(page.Number, []);
        }

        var lines = DocstrumBoundingBoxes.Instance.GetBlocks(words)
            .SelectMany(b => b.TextLines)
            .Select((line, index) => new TextLine(index + 1, line.Text, Normalize(line.BoundingBox, page)))
            .ToList();
        return new DocumentPage(page.Number, lines);
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
}
