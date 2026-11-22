using System.Globalization;
using System.Text;
using Idefix.Core.Indexing;

namespace Idefix.Core.Agent;

public static class PromptComposer
{
    public const string SearchFunctionName = "search_plugins";

    public static string SystemPrompt(string prePrompt, bool deepThinking, IReadOnlyCollection<string> plugins)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(prePrompt))
        {
            builder.AppendLine(prePrompt.Trim()).AppendLine();
        }

        builder.AppendLine("You are Idefix, an assistant that answers strictly from the user's local knowledge plugins.");
        builder.Append(CultureInfo.InvariantCulture, $"Available knowledge plugins: {(plugins.Count == 0 ? "(none)" : string.Join(", ", plugins))}.").AppendLine();
        builder.Append(CultureInfo.InvariantCulture, $"Use the `{SearchFunctionName}` tool to retrieve passages before answering.").AppendLine();
        builder.AppendLine(deepThinking
            ? "If the retrieved passages are insufficient, refine the query and call the tool again (up to 4 rounds) before answering."
            : "Call the tool once, then answer with what was found.");
        builder.AppendLine("Cite every factual statement with the source in the exact form [FileName.pdf, p. N].");
        builder.AppendLine("If the knowledge plugins do not contain the answer, say so plainly.");
        builder.AppendLine("Answer in the language of the user's question.");
        return builder.ToString();
    }

    public static string FormatResults(IReadOnlyList<ScoredChunk> results)
    {
        if (results.Count == 0)
        {
            return "No matching passages found.";
        }

        var builder = new StringBuilder();
        foreach (var result in results)
        {
            var chunk = result.Chunk;
            builder.Append(CultureInfo.InvariantCulture, $"[{chunk.FileName}, p. {chunk.Page}] (plugin {chunk.PluginName}, lines {chunk.FirstLine}-{chunk.LastLine}, score {result.Score:0.00})")
                .AppendLine()
                .AppendLine(chunk.Text)
                .AppendLine();
        }

        return builder.ToString().TrimEnd();
    }
}
