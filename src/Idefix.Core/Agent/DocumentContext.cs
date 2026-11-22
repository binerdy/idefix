using System.Globalization;
using System.Text;
using Idefix.Core.Chat;
using Idefix.Core.Knowledge;
using Idefix.Core.Markdown;

namespace Idefix.Core.Agent;

public sealed record DocumentContext(string Text, ContextSummary Summary)
{
    // Roughly four characters per token for European languages; good enough for a budget check.
    private const int CharactersPerToken = 4;

    public static int EstimateTokens(string text) => (text.Length + CharactersPerToken - 1) / CharactersPerToken;

    public static DocumentContext Build(IReadOnlyList<LoadedDocument> documents)
    {
        var builder = new StringBuilder();
        foreach (var loaded in documents)
        {
            var fileName = loaded.Document.FileName;
            builder.Append(CultureInfo.InvariantCulture, $"=== {fileName} (plugin {loaded.Document.PluginName}) ===").AppendLine();
            foreach (var page in loaded.Content.Pages.Where(p => p.Lines.Count > 0))
            {
                builder.Append(CultureInfo.InvariantCulture, $"--- [{fileName}, p. {page.Number}] ---").AppendLine();
                builder.AppendLine(page.Text);
            }

            builder.AppendLine();
        }

        var text = builder.ToString();
        var summary = new ContextSummary(documents.Select(d => d.Document.Key).ToList(), EstimateTokens(text));
        return new DocumentContext(text, summary);
    }

    public static DocumentContext Build(IReadOnlyList<DocumentSection> sections, int totalSections, IReadOnlyList<SelectionRound>? rounds = null)
    {
        var builder = new StringBuilder();
        foreach (var section in sections)
        {
            builder.Append(CultureInfo.InvariantCulture, $"=== {section.FileName} › {section.Title} (plugin {section.PluginName}) ===").AppendLine();
            builder.AppendLine(MarkdownSplitter.ToContext(section)).AppendLine();
        }

        var text = builder.ToString();
        var summary = new ContextSummary(sections.Select(s => s.Label).ToList(), EstimateTokens(text), totalSections, rounds);
        return new DocumentContext(text, summary);
    }
}
