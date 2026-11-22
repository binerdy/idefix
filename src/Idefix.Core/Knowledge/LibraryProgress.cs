namespace Idefix.Core.Knowledge;

public sealed record LibraryProgress(int Total, int Completed)
{
    public bool IsComplete => this.Completed >= this.Total;
}
