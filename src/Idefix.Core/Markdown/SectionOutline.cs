using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Idefix.Core.Markdown;

public static partial class SectionOutline
{
    private const int PreviewLength = 140;

    public static string Id(int position) => string.Create(CultureInfo.InvariantCulture, $"S{position + 1}");

    public static string Build(IReadOnlyList<DocumentSection> sections)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < sections.Count; i++)
        {
            var section = sections[i];
            builder.Append(CultureInfo.InvariantCulture, $"{Id(i)} | {section.FileName} | {section.Pages} | {section.Title} | ~{section.EstimatedTokens} tokens | {Preview(section)}")
                .AppendLine();
        }

        return builder.ToString();
    }

    public static IReadOnlyList<DocumentSection>? ParseSelection(string reply, IReadOnlyList<DocumentSection> sections)
    {
        var json = JsonList().Match(reply);
        if (!json.Success)
        {
            return null;
        }

        return IdPattern().Matches(json.Value)
            .Select(m => int.Parse(m.Groups["n"].Value, CultureInfo.InvariantCulture) - 1)
            .Where(i => i >= 0 && i < sections.Count)
            .Distinct()
            .Order()
            .Select(i => sections[i])
            .ToList();
    }

    private static string Preview(DocumentSection section)
    {
        var text = string.Join(' ', section.Markdown
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("<!--", StringComparison.Ordinal) && l[0] != '#'));
        return text.Length <= PreviewLength ? text : string.Concat(text.AsSpan(0, PreviewLength), "…");
    }

    [GeneratedRegex(@"\[[^\[\]]*\]", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex JsonList();

    [GeneratedRegex(@"S(?<n>\d{1,5})\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex IdPattern();
}
