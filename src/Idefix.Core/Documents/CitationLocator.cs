using Idefix.Core.Knowledge;

namespace Idefix.Core.Documents;

public static class CitationLocator
{
    private const int MinimumWordLength = 3;
    private const int MaximumLines = 4;

    private static readonly char[] Separators =
        [' ', '\n', '\r', '\t', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '"', '\'', '«', '»', '„', '“', '”', '-', '/', '*'];

    public static IReadOnlyList<Citation> Resolve(string answer, IReadOnlyList<LoadedDocument> documents)
    {
        var citations = new List<Citation>();
        var claimStart = 0;
        foreach (var match in CitationParser.Find(answer))
        {
            var claim = Claim(answer, claimStart, match.Index);
            claimStart = match.Index + match.Length;
            if (citations.Exists(c => SameSource(c, match.Citation)))
            {
                continue;
            }

            citations.Add(Locate(match.Citation, claim, documents));
        }

        return citations;
    }

    private static Citation Locate(Citation citation, string claim, IReadOnlyList<LoadedDocument> documents)
    {
        var document = documents.FirstOrDefault(d => string.Equals(d.Document.FileName, citation.FileName, StringComparison.OrdinalIgnoreCase));
        if (document is null)
        {
            return citation;
        }

        var located = citation with { DocumentKey = document.Document.Key };
        var page = document.Content.Pages.FirstOrDefault(p => p.Number == citation.Page);
        var words = Words(claim);
        if (page is null || words.Count == 0)
        {
            return located;
        }

        var scores = page.Lines.Select(line => Words(line.Text).Count(words.Contains)).ToList();
        var best = scores.Count == 0 ? 0 : scores.Max();
        if (best == 0)
        {
            return located;
        }

        var bestIndex = scores.IndexOf(best);
        var first = bestIndex;
        var last = bestIndex;
        var threshold = (best + 1) / 2;
        while (last - first + 1 < MaximumLines && last + 1 < scores.Count && scores[last + 1] >= threshold)
        {
            last++;
        }

        while (last - first + 1 < MaximumLines && first > 0 && scores[first - 1] >= threshold)
        {
            first--;
        }

        return located with { FirstLine = page.Lines[first].Number, LastLine = page.Lines[last].Number };
    }

    private static string Claim(string answer, int start, int citationIndex)
    {
        var text = answer[start..citationIndex];
        var boundary = text.TrimEnd().LastIndexOfAny(['.', '!', '?', '\n'], Math.Max(0, text.TrimEnd().Length - 2));
        return boundary < 0 ? text : text[(boundary + 1)..];
    }

    private static HashSet<string> Words(string text) =>
        text.ToUpperInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= MinimumWordLength)
            .ToHashSet(StringComparer.Ordinal);

    private static bool SameSource(Citation left, Citation right) =>
        left.Page == right.Page && string.Equals(left.FileName, right.FileName, StringComparison.OrdinalIgnoreCase);
}
