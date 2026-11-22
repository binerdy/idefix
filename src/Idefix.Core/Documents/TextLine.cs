namespace Idefix.Core.Documents;

public sealed record TextLine(int Number, string Text, TextBounds? Bounds, int HeadingLevel = 0)
{
    public bool IsHeading => this.HeadingLevel > 0;
}
