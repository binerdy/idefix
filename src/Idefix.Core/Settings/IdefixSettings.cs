using Idefix.Core.Agent;

namespace Idefix.Core.Settings;

public sealed record IdefixSettings(
    string FirstName,
    string RootDirectory,
    LlmSettings Llm,
    string PrePrompt,
    ContextMode ContextMode,
    AppTheme Theme,
    AppLanguage Language,
    bool NightlyUpdates,
    IReadOnlyList<string> DisabledPlugins)
{
    public static IdefixSettings CreateDefault(string userProfileDirectory, string firstName) => new(
        firstName,
        Path.Combine(userProfileDirectory, "IdefixKnowledge"),
        LlmSettings.Default,
        string.Empty,
        ContextMode.Chunks,
        AppTheme.System,
        AppLanguage.System,
        NightlyUpdates: false,
        DisabledPlugins: []);
}
