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

            lines.Add(new TextLine(lines.Count + 1, paragraph.InnerText, Bounds: null));

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

    private static bool StartsNewPage(Paragraph paragraph) =>
        paragraph.ParagraphProperties?.PageBreakBefore is { } pageBreak && (pageBreak.Val is null || pageBreak.Val.Value);

    private static bool ContainsPageBreak(Paragraph paragraph) =>
        paragraph.Descendants<Break>().Any(b => b.Type is not null && b.Type.Value == BreakValues.Page);
}
