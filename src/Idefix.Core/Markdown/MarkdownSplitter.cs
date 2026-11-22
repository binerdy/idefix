using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Idefix.Core.Agent;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;

namespace Idefix.Core.Markdown;

public static partial class MarkdownSplitter
{
    public const int GeneratorVersion = 1;
    public const int MaxSectionTokens = 2_000;
    public const int MinSectionTokens = 60;

    public static IReadOnlyList<DocumentSection> Split(LoadedDocument loaded)
    {
        var lines = loaded.Content.Pages
            .SelectMany(page => page.Lines.Where(l => !string.IsNullOrWhiteSpace(l.Text)).Select(line => (Page: page.Number, Line: line)))
            .ToList();
        if (lines.Count == 0)
        {
            return [];
        }

        var drafts = lines.Exists(l => l.Line.IsHeading) ? ByHeadings(lines) : ByPages(lines);
        var sized = drafts.SelectMany(SplitLarge).ToList();
        var merged = MergeSmall(sized);

        return merged
            .Select((draft, i) => new DocumentSection(
                loaded.Document.Key,
                loaded.Document.PluginName,
                loaded.Document.FileName,
                i + 1,
                draft.Title(loaded.Document.FileName),
                draft.Lines[0].Page,
                draft.Lines[^1].Page,
                draft.ToMarkdown()))
            .ToList();
    }

    public static string PageMarker(int page) => string.Create(CultureInfo.InvariantCulture, $"<!-- page {page} -->");

    public static string ToContext(DocumentSection section) =>
        PageMarkerPattern().Replace(section.Markdown, m => $"--- [{section.FileName}, p. {m.Groups["page"].Value}] ---");

    private static List<Draft> ByHeadings(List<(int Page, TextLine Line)> lines)
    {
        var drafts = new List<Draft>();
        var current = new Draft();
        foreach (var entry in lines)
        {
            if (entry.Line.IsHeading && current.HasBody)
            {
                drafts.Add(current);
                current = new Draft();
            }

            current.Lines.Add(entry);
        }

        drafts.Add(current);
        return drafts;
    }

    private static List<Draft> ByPages(List<(int Page, TextLine Line)> lines) =>
        lines.GroupBy(l => l.Page).Select(g => new Draft(g)).ToList();

    private static IEnumerable<Draft> SplitLarge(Draft draft)
    {
        if (DocumentContext.EstimateTokens(draft.ToMarkdown()) <= MaxSectionTokens)
        {
            yield return draft;
            yield break;
        }

        var part = new Draft();
        foreach (var entry in draft.Lines)
        {
            if (part.Lines.Count > 0 && DocumentContext.EstimateTokens(part.ToMarkdown() + entry.Line.Text) > MaxSectionTokens)
            {
                yield return part;
                part = new Draft { Continues = draft.Lines[0].Line.IsHeading ? draft.Lines[0].Line.Text : null };
            }

            part.Lines.Add(entry);
        }

        yield return part;
    }

    private static List<Draft> MergeSmall(List<Draft> drafts)
    {
        var merged = new List<Draft>();
        foreach (var draft in drafts)
        {
            var previous = merged.Count > 0 ? merged[^1] : null;
            var small = DocumentContext.EstimateTokens(draft.ToMarkdown()) < MinSectionTokens;
            var startsWithHeading = draft.Lines[0].Line.IsHeading;
            if (previous is not null && small && !startsWithHeading
                && DocumentContext.EstimateTokens(previous.ToMarkdown() + draft.ToMarkdown()) <= MaxSectionTokens)
            {
                previous.Lines.AddRange(draft.Lines);
            }
            else
            {
                merged.Add(draft);
            }
        }

        return merged;
    }

    [GeneratedRegex(@"<!-- page (?<page>\d+) -->", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageMarkerPattern();

    private sealed class Draft
    {
        public Draft()
        {
        }

        public Draft(IEnumerable<(int Page, TextLine Line)> lines) => this.Lines.AddRange(lines);

        public List<(int Page, TextLine Line)> Lines { get; } = [];

        public string? Continues { get; init; }

        public bool HasBody => this.Lines.Exists(l => !l.Line.IsHeading);

        public string Title(string fileName)
        {
            var heading = this.Lines.Find(l => l.Line.IsHeading).Line;
            if (heading is not null)
            {
                return heading.Text.Trim();
            }

            return this.Continues is { } title
                ? title.Trim()
                : string.Create(CultureInfo.InvariantCulture, $"{Path.GetFileNameWithoutExtension(fileName)}, p. {this.Lines[0].Page}");
        }

        public string ToMarkdown()
        {
            var builder = new StringBuilder();
            var page = 0;
            foreach (var (linePage, line) in this.Lines)
            {
                if (linePage != page)
                {
                    page = linePage;
                    builder.AppendLine(PageMarker(page));
                }

                builder.AppendLine(line.IsHeading ? $"{new string('#', Math.Min(line.HeadingLevel, 6))} {line.Text.Trim()}" : line.Text);
            }

            return builder.ToString();
        }
    }
}
