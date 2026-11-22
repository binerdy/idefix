using Idefix.Core.Chat;

namespace Idefix.Core.Agent;

public sealed record AnswerRequest(
    string Question,
    IReadOnlyList<ChatMessage> History,
    IReadOnlyList<string> Plugins,
    bool DeepThinking = true);
