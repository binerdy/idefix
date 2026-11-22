namespace Idefix.Core.Chat;

public sealed record ChatSession(
    Guid Id,
    string Title,
    Guid? ProjectId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyList<string> DisabledPlugins,
    bool DeepThinking = true)
{
    public static ChatSession Start(Guid? projectId, TimeProvider time) =>
        new(Guid.NewGuid(), string.Empty, projectId, time.GetUtcNow(), [], []);

    public ChatSession Append(ChatMessage message)
    {
        var title = string.IsNullOrWhiteSpace(this.Title) && message.Role == ChatRole.User
            ? SessionTitle.From(message.Content)
            : this.Title;
        return this with { Title = title, Messages = [.. this.Messages, message] };
    }
}
