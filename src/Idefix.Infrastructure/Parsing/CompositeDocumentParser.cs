using Idefix.Core.Documents;

namespace Idefix.Infrastructure.Parsing;

public sealed class CompositeDocumentParser(IReadOnlyList<IDocumentParser> parsers) : IDocumentParser
{
    public static CompositeDocumentParser CreateDefault() => new([new PdfDocumentParser(), new DocxDocumentParser()]);

    public bool CanParse(string path) => parsers.Any(p => p.CanParse(path));

    public ParsedDocument Parse(string path)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(path))
            ?? throw new NotSupportedException($"No parser for '{Path.GetExtension(path)}'.");
        return parser.Parse(path);
    }
}
