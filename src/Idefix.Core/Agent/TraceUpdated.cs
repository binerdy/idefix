using Idefix.Core.Chat;

namespace Idefix.Core.Agent;

public sealed record TraceUpdated(TraceStep Step) : AnswerEvent;
