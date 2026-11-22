namespace Idefix.Core.Chat;

public static class SessionTitle
{
    public const int MaxLength = 48;

    public static string From(string firstQuestion)
    {
        var singleLine = string.Join(' ', firstQuestion.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= MaxLength ? singleLine : string.Concat(singleLine.AsSpan(0, MaxLength - 1).TrimEnd(), "…");
    }
}
