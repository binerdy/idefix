using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Idefix.Core.Agent;
using Idefix.Core.Markdown;
using Microsoft.SemanticKernel;

namespace Idefix.Infrastructure.Agent;

internal sealed class SectionTools
{
    private readonly SectionCollector collector;
    private readonly ISectionSearch search;
    private readonly int limit;

    public SectionTools(SectionCollector collector, ISectionSearch search, int limit)
    {
        this.collector = collector;
        this.search = search;
        this.limit = limit;
        this.Functions =
        [
            KernelFunctionFactory.CreateFromMethod(this.SearchSections, "search_sections", "Searches all document sections by keywords and returns the best ones that you have not seen yet."),
            KernelFunctionFactory.CreateFromMethod(this.ListSections, "list_sections", "Returns the table of contents of all sections. Use it when a search does not find what you need, for example because the documents use another language or other words."),
            KernelFunctionFactory.CreateFromMethod(this.ReadSections, "read_sections", "Returns sections listed in the table of contents."),
        ];
    }

    public IReadOnlyList<KernelFunction> Functions { get; }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "The model gets the error and can correct its call.")]
    public async Task<string> InvokeAsync(FunctionCallContent call, Kernel kernel, CancellationToken cancellationToken)
    {
        var function = this.Functions.FirstOrDefault(f => string.Equals(f.Name, call.FunctionName, StringComparison.Ordinal));
        if (function is null)
        {
            return $"Unknown function {call.FunctionName}.";
        }

        try
        {
            var result = await function.InvokeAsync(kernel, call.Arguments, cancellationToken);
            return result.GetValue<string>() ?? string.Empty;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"The call failed: {ex.Message}";
        }
    }

    private static string Describe(List<DocumentSection> sections, int total) =>
        sections.Count == 0 ? "No further sections found." : DocumentContext.Build(sections, total).Text;

    private string SearchSections([Description("Keywords in the language of the documents. Try synonyms and parts of compound words.")] string query)
    {
        var available = this.collector.Available;

        // Searching everything and dropping known sections afterwards keeps the index and its word weights unchanged.
        var unseen = this.search.Search(query, available, available.Count).Where(s => !this.collector.IsChosen(s)).Take(this.limit);
        return Describe(this.collector.Add(unseen), available.Count);
    }

    private string ListSections()
    {
        var outline = SectionOutline.Build(this.collector.Available);
        return DocumentContext.EstimateTokens(outline) > this.collector.RemainingTokens()
            ? "The table of contents is too long to show. Use search_sections instead."
            : outline;
    }

    private string ReadSections([Description("Section ids from the table of contents, separated by commas, for example: S3, S7")] string ids)
    {
        var available = this.collector.Available;
        var picked = SectionOutline.ParseSelection($"[{ids.Trim('[', ']', ' ')}]", available) ?? [];
        return Describe(this.collector.Add(picked), available.Count);
    }
}
