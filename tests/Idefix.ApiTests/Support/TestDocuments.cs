using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using PdfPageSize = UglyToad.PdfPig.Content.PageSize;

namespace Idefix.ApiTests.Support;

internal static class TestDocuments
{
    public static string WriteDocx(string path, params string[] pages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var document = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        var body = new Body();
        for (var i = 0; i < pages.Length; i++)
        {
            foreach (var line in Lines(pages[i]))
            {
                var (text, level) = Heading(line);
                var paragraph = Paragraph(new Text(text));
                if (level > 0)
                {
                    paragraph.PrependChild(new ParagraphProperties { ParagraphStyleId = new ParagraphStyleId { Val = $"Heading{level}" } });
                }

                body.AppendChild(paragraph);
            }

            if (i < pages.Length - 1)
            {
                body.AppendChild(Paragraph(new Break { Type = BreakValues.Page }));
            }
        }

        main.Document = new Document { Body = body };
        main.Document.Save();
        return path;
    }

    public static string WritePdf(string path, params string[] pages)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var lines in pages)
        {
            var page = builder.AddPage(PdfPageSize.A4);
            var y = 760d;
            foreach (var line in Lines(lines))
            {
                var (text, level) = Heading(line);
                page.AddText(text, level switch { 1 => 22, 2 => 16, _ => 12 }, new PdfPoint(60, y), font);
                y -= 40;
            }
        }

        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    public static void Touch(string path, DateTime lastWriteUtc) => File.SetLastWriteTimeUtc(path, lastWriteUtc);

    private static string[] Lines(string page) => page.Split('\n');

    private static (string Text, int Level) Heading(string line)
    {
        var level = line.TakeWhile(c => c == '#').Count();
        return level > 0 && line.Length > level && line[level] == ' ' ? (line[(level + 1)..], level) : (line, 0);
    }

    private static Paragraph Paragraph(OpenXmlElement content)
    {
        var run = new Run();
        run.AppendChild(content);
        var paragraph = new Paragraph();
        paragraph.AppendChild(run);
        return paragraph;
    }
}
