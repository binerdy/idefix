namespace Idefix.Core.Indexing;

public sealed record IndexingProgress(int Total, int Completed, string? CurrentDocument)
{
    public bool IsComplete => this.Completed >= this.Total;
}
