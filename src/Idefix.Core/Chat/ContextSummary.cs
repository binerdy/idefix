namespace Idefix.Core.Chat;

public sealed record ContextSummary(
    IReadOnlyList<string> Documents,
    int EstimatedTokens,
    int TotalSections = 0,
    IReadOnlyList<SelectionRound>? Rounds = null);
