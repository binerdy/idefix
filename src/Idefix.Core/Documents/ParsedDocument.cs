namespace Idefix.Core.Documents;

public sealed record ParsedDocument(IReadOnlyList<DocumentPage> Pages)
{
    public string Text => string.Join("\n\n", this.Pages.Select(p => p.Text));
}
