namespace Idefix.Core.Documents;

public sealed record Citation(string FileName, int Page, string? DocumentKey = null, int FirstLine = 0, int LastLine = 0)
{
    public override string ToString() => $"[{this.FileName}, p. {this.Page}]";
}
