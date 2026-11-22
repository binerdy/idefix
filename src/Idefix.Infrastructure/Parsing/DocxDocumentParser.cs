using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Idefix.Core.Documents;

namespace Idefix.Infrastructure.Parsing;

public sealed class DocxDocumentParser : IDocumentParser
{
    public bool CanParse(string path) => string.Equals(Path.GetExtension(path), ".docx", StringComparison.OrdinalIgnoreCase);

    public ParsedDocument Parse(string path)
    {
        using var document = WordprocessingDocument.Open(path, isEditable: false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return new ParsedDocument([]);
        }

        var pages = new List<DocumentPage>();
        var lines = new List<TextLine>();
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            if (StartsNewPage(paragraph) && lines.Count > 0)
            {
                pages.Add(new DocumentPage(pages.Count + 1, lines));
                lines = [];
            }

            lines.Add(new TextLine(lines.Count + 1, paragraph.InnerText, Bounds: null, HeadingLevel(paragraph)));

            if (ContainsPageBreak(paragraph))
            {
                pages.Add(new DocumentPage(pages.Count + 1, lines));
                lines = [];
            }
        }

        if (lines.Count > 0 || pages.Count == 0)
        {
            pages.Add(new DocumentPage(pages.Count + 1, lines));
        }

        return new ParsedDocument(pages);
    }

    private static int HeadingLevel(Paragraph paragraph)
    {
        var properties = paragraph.ParagraphProperties;
        if (properties?.OutlineLevel?.Val is { } outline && outline.Value < 9)
        {
            return outline.Value + 1;
        }

        var style = properties?.ParagraphStyleId?.Val?.Value ?? string.Empty;
        if (style.Equals("Title", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        // Word stores localized style ids, e.g. "Heading2" in English and "berschrift2" in German documents.
        var isHeading = style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("berschrift", StringComparison.OrdinalIgnoreCase)
            || style.StartsWith("Überschrift", StringComparison.OrdinalIgnoreCase);
        if (!isHeading)
        {
            return 0;
        }

        var digits = new string(style.Where(char.IsAsciiDigit).ToArray());
        return int.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var level) && level > 0 ? level : 1;
    }

    private static bool StartsNewPage(Paragraph paragraph) =>
        paragraph.ParagraphProperties?.PageBreakBefore is { } pageBreak && (pageBreak.Val is null || pageBreak.Val.Value);

    private static bool ContainsPageBreak(Paragraph paragraph) =>
        paragraph.Descendants<Break>().Any(b => b.Type is not null && b.Type.Value == BreakValues.Page);
}
