using Idefix.Core.Chat;

namespace Idefix.Core.Agent;

public sealed record SectionsPicked(SelectionRound Round, int TotalSections) : AnswerEvent;
