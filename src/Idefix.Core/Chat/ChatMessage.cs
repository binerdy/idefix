using Idefix.Core.Documents;

namespace Idefix.Core.Chat;

public sealed record ChatMessage(
    ChatRole Role,
    string Content,
    DateTimeOffset Timestamp,
    ContextSummary? Context,
    IReadOnlyList<Citation> Citations)
{
    public static ChatMessage FromUser(string content, TimeProvider time) =>
        new(ChatRole.User, content, time.GetUtcNow(), Context: null, []);
}
