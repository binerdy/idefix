using Idefix.Core.Agent;
using Idefix.Core.Chat;
using Idefix.Core.Markdown;

namespace Idefix.Infrastructure.Agent;

internal sealed class SectionCollector(IReadOnlyList<DocumentSection> available, int budget)
{
    private readonly List<DocumentSection> chosen = [];
    private readonly List<DocumentSection> added = [];
    private readonly List<SelectionRound> rounds = [];

    public IReadOnlyList<DocumentSection> Available => available;

    public int RemainingTokens() => budget - this.chosen.Sum(s => s.EstimatedTokens);

    public bool IsChosen(DocumentSection section) => this.chosen.Contains(section);

    public DocumentContext BuildContext()
    {
        var order = available.Select((section, position) => (section, position)).ToDictionary(p => p.section, p => p.position);
        return DocumentContext.Build([.. this.chosen.OrderBy(s => order[s])], available.Count, this.rounds.Count > 0 ? [.. this.rounds] : null);
    }

    public List<DocumentSection> Add(IEnumerable<DocumentSection> sections)
    {
        var kept = new List<DocumentSection>();
        foreach (var section in sections.Except(this.chosen).ToList())
        {
            if (section.EstimatedTokens <= this.RemainingTokens())
            {
                this.chosen.Add(section);
                kept.Add(section);
            }
        }

        this.added.AddRange(kept);
        return kept;
    }

    public SelectionRound? CompleteRound(int number)
    {
        if (this.added.Count == 0)
        {
            return null;
        }

        var round = new SelectionRound(number, this.added.Select(s => s.Label).ToList());
        this.added.Clear();
        this.rounds.Add(round);
        return round;
    }
}
