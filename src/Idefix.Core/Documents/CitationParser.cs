using System.Globalization;
using System.Text.RegularExpressions;

namespace Idefix.Core.Documents;

public static partial class CitationParser
{
    public static IReadOnlyList<CitationMatch> Find(string text)
    {
        return CitationPattern()
            .Matches(text)
            .Select(m => new CitationMatch(
                new Citation(m.Groups["file"].Value.Trim(), int.Parse(m.Groups["page"].Value, CultureInfo.InvariantCulture)),
                m.Index,
                m.Length))
            .ToList();
    }

    [GeneratedRegex(@"\[(?<file>[^\[\],]+?\.(?:pdf|docx)),\s*(?:p|S)\.\s*(?<page>\d{1,5})\]", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CitationPattern();
}
