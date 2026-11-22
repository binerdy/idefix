namespace Idefix.Core.Chat;

public sealed record TraceStep(int Round, string Query, IReadOnlyList<string> Plugins, int ResultCount, IReadOnlyList<string> Sources, string? Error = null);
