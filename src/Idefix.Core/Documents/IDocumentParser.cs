namespace Idefix.Core.Documents;

public interface IDocumentParser
{
    bool CanParse(string path);

    ParsedDocument Parse(string path);
}
