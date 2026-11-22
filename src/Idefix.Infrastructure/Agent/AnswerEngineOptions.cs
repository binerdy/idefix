using Idefix.Core.Agent;

namespace Idefix.Infrastructure.Agent;

public sealed record AnswerEngineOptions(
    string RootDirectory,
    string PrePrompt,
    ContextMode ContextMode,
    int ResultsPerSearch = 6,
    int MaxSearchRounds = 4,
    int MaxFullFiles = 3,
    int MaxFullFileCharacters = 40_000,
    int HistoryMessages = 10);
