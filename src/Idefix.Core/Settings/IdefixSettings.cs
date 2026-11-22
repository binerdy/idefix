namespace Idefix.Core.Settings;

public sealed record IdefixSettings(
    string FirstName,
    string RootDirectory,
    LlmSettings Llm,
    string PrePrompt,
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
        AppTheme.System,
        AppLanguage.System,
        NightlyUpdates: false,
        DisabledPlugins: []);
}
