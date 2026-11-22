namespace Idefix.Core.Documents;

public sealed record DocumentPage(int Number, IReadOnlyList<TextLine> Lines)
{
    public string Text => string.Join('\n', this.Lines.Select(l => l.Text));
}
