namespace Idefix.Infrastructure.Agent;

public sealed record AnswerEngineOptions(
    string PrePrompt,
    int MaxContextTokens,
    int HistoryMessages = 10,
    int SelectionThresholdTokens = 8_000,
    int MaxRounds = 3);
