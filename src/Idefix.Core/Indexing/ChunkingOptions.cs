namespace Idefix.Core.Indexing;

public sealed record ChunkingOptions(int MaxCharacters = 1200, int OverlapLines = 2)
{
    public static ChunkingOptions Default { get; } = new();
}
