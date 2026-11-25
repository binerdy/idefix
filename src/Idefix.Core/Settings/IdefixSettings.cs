namespace Idefix.Core.Settings;

public sealed record IdefixSettings(
    string FirstName,
    string RootDirectory,
    LlmSettings Llm,
    string PrePrompt,
    AppLanguage Language,
    bool NightlyUpdates,
    IReadOnlyList<string> DisabledPlugins)
{
    public static IdefixSettings CreateDefault(string userProfileDirectory, string firstName) => new(
        firstName,
        Path.Combine(userProfileDirectory, "IdefixKnowledge"),
        LlmSettings.Default,
        string.Empty,
        AppLanguage.System,
        NightlyUpdates: false,
        DisabledPlugins: []);
}
