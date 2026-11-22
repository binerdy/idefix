namespace Idefix.Core.Markdown;

public sealed record DocumentSection(
    string DocumentKey,
    string PluginName,
    string FileName,
    int Index,
    string Title,
    int FirstPage,
    int LastPage,
    string Markdown)
{
    public int EstimatedTokens => Agent.DocumentContext.EstimateTokens(this.Markdown);

    public string Pages => this.FirstPage == this.LastPage
        ? $"p. {this.FirstPage}"
        : $"p. {this.FirstPage}-{this.LastPage}";

    public string Label => $"{this.FileName} › {this.Title} ({this.Pages})";
}
