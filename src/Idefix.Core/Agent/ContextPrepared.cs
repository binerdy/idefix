using Idefix.Core.Chat;

namespace Idefix.Core.Agent;

public sealed record ContextPrepared(ContextSummary Summary) : AnswerEvent;
