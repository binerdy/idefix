using Idefix.App.Services;
using Idefix.Core.Chat;

namespace Idefix.App.ViewModels;

public sealed class TraceStepViewModel(TraceStep step)
{
    public string Headline { get; } = Strings.Format("TraceStepFormat", step.Query, step.ResultCount);

    public string Sources { get; } = step.Error ?? string.Join(" · ", step.Sources);
}
