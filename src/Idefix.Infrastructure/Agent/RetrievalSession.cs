using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using Idefix.Core.Agent;
using Idefix.Core.Chat;
using Idefix.Core.Documents;
using Idefix.Core.Indexing;
using Microsoft.SemanticKernel;

namespace Idefix.Infrastructure.Agent;

public sealed class RetrievalSession(
    IKnowledgeSearch search,
    IDocumentParser parser,
    IReadOnlyList<string> plugins,
    AnswerEngineOptions options,
    Action<TraceStep> onStep)
{
    private readonly List<ScoredChunk> retrieved = [];
    private readonly List<TraceStep> steps = [];

    public IReadOnlyList<TraceStep> Trace => this.steps;

    [KernelFunction(PromptComposer.SearchFunctionName)]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed search is reported to the model and the trace instead of aborting the answer.")]
    [Description("Searches the user's enabled knowledge plugins and returns the most relevant passages with their source and page.")]
    public async Task<string> SearchPluginsAsync(
        [Description("A focused search query, phrased in the language of the documents.")] string query,
        CancellationToken cancellationToken)
    {
        if (this.steps.Count >= options.MaxSearchRounds)
        {
            return "Search limit reached. Answer now using the passages already retrieved.";
        }

        IReadOnlyList<ScoredChunk> results;
        try
        {
            results = await search.SearchAsync(query, [.. plugins], options.ResultsPerSearch, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failed = new TraceStep(this.steps.Count + 1, query, plugins, 0, [], ex.Message);
            this.steps.Add(failed);
            onStep(failed);
            return $"The search failed: {ex.Message}. Tell the user that the knowledge plugins could not be searched.";
        }

        this.retrieved.AddRange(results);

        var step = new TraceStep(
            this.steps.Count + 1,
            query,
            plugins,
            results.Count,
            results.Select(r => r.Chunk).Select(c => string.Create(CultureInfo.InvariantCulture, $"{c.FileName}, p. {c.Page}")).Distinct(StringComparer.Ordinal).ToList());
        this.steps.Add(step);
        onStep(step);

        return options.ContextMode == ContextMode.FullFiles
            ? await Task.Run(() => this.FormatFullFiles(results), cancellationToken)
            : PromptComposer.FormatResults(results);
    }

    public IReadOnlyList<Citation> ResolveCitations(string answer)
    {
        return CitationParser.Find(answer)
            .Select(m => m.Citation)
            .DistinctBy(c => (c.FileName.ToUpperInvariant(), c.Page))
            .Select(this.Resolve)
            .ToList();
    }

    private Citation Resolve(Citation citation)
    {
        var matches = this.retrieved
            .Select(r => r.Chunk)
            .Where(c => string.Equals(c.FileName, citation.FileName, StringComparison.OrdinalIgnoreCase) && c.Page == citation.Page)
            .ToList();
        if (matches.Count == 0)
        {
            var document = this.retrieved.Select(r => r.Chunk).FirstOrDefault(c => string.Equals(c.FileName, citation.FileName, StringComparison.OrdinalIgnoreCase));
            return citation with { DocumentKey = document?.DocumentKey };
        }

        return citation with
        {
            DocumentKey = matches[0].DocumentKey,
            FirstLine = matches.Min(c => c.FirstLine),
            LastLine = matches.Max(c => c.LastLine),
        };
    }

    private string FormatFullFiles(IReadOnlyList<ScoredChunk> results)
    {
        var builder = new StringBuilder();
        foreach (var chunk in results.Select(r => r.Chunk).DistinctBy(c => c.DocumentKey).Take(options.MaxFullFiles))
        {
            var path = Path.Combine(options.RootDirectory, chunk.DocumentKey.Replace('/', Path.DirectorySeparatorChar));
            builder.Append(CultureInfo.InvariantCulture, $"=== {chunk.FileName} (plugin {chunk.PluginName}) ===").AppendLine();
            var length = 0;
            foreach (var page in parser.Parse(path).Pages)
            {
                var text = page.Text;
                if (length + text.Length > options.MaxFullFileCharacters)
                {
                    builder.AppendLine("[truncated]");
                    break;
                }

                builder.Append(CultureInfo.InvariantCulture, $"--- [{chunk.FileName}, p. {page.Number}] ---").AppendLine().AppendLine(text);
                length += text.Length;
            }

            builder.AppendLine();
        }

        return builder.Length == 0 ? "No matching documents found." : builder.ToString();
    }
}
