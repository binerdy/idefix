namespace Idefix.Core.Settings;

public static class SettingsValidator
{
    public static IReadOnlyList<SettingsIssue> Validate(IdefixSettings settings)
    {
        var issues = new List<SettingsIssue>();
        if (string.IsNullOrWhiteSpace(settings.RootDirectory))
        {
            issues.Add(SettingsIssue.RootDirectoryMissing);
        }

        if (string.IsNullOrWhiteSpace(settings.Llm.ApiKey))
        {
            issues.Add(SettingsIssue.ApiKeyMissing);
        }

        if (string.IsNullOrWhiteSpace(settings.Llm.ChatModel))
        {
            issues.Add(SettingsIssue.ChatModelMissing);
        }

        if (settings.Llm.MaxContextTokens < 1_000)
        {
            issues.Add(SettingsIssue.ContextTooSmall);
        }

        return issues;
    }
}
