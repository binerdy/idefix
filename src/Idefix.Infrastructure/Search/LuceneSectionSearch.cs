using Idefix.Core.Markdown;

namespace Idefix.Infrastructure.Search;

public sealed class LuceneSectionSearch : ISectionSearch, IDisposable
{
    private const float WordShare = 0.7f;
    private const float GramShare = 0.3f;
    private const float MinScore = 0.05f;

    private readonly Lock gate = new();
    private SectionIndex? index;

    public int Builds { get; private set; }

    public IReadOnlyList<DocumentSection> Search(string query, IReadOnlyList<DocumentSection> sections, int limit)
    {
        lock (this.gate)
        {
            var current = this.IndexFor(sections);
            var words = current.MatchWords(query);

            // Letter groups alone match almost any text; without a single word in common the search has no opinion.
            if (words.Count == 0)
            {
                return [];
            }

            var grams = current.MatchGrams(query);
            var bestWord = words.Values.Max();
            var bestGram = grams.Values.DefaultIfEmpty(1f).Max();
            return words.Keys.Union(grams.Keys)
                .Select(i => (Position: i, Score: (WordShare * words.GetValueOrDefault(i) / bestWord) + (GramShare * grams.GetValueOrDefault(i) / bestGram)))
                .Where(hit => hit.Score >= MinScore)
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => hit.Position)
                .Take(limit)
                .Select(hit => current.Sections[hit.Position])
                .ToList();
        }
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            this.index?.Dispose();
            this.index = null;
        }
    }

    private SectionIndex IndexFor(IReadOnlyList<DocumentSection> sections)
    {
        if (this.index is { } existing && existing.Sections.SequenceEqual(sections))
        {
            return existing;
        }

        this.index?.Dispose();
        this.index = null;
        this.index = new SectionIndex(sections);
        this.Builds++;
        return this.index;
    }
}
