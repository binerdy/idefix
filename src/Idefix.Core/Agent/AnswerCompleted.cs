using Idefix.Core.Chat;

namespace Idefix.Core.Agent;

public sealed record AnswerCompleted(ChatMessage Message) : AnswerEvent;
