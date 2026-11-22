using Idefix.Core.Documents;

namespace Idefix.App.Controls;

public sealed class CitationEventArgs(Citation citation) : EventArgs
{
    public Citation Citation { get; } = citation;
}
