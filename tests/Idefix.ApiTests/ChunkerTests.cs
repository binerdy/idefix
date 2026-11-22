using Idefix.Core.Documents;
using Idefix.Core.Indexing;
using Idefix.Core.Knowledge;

namespace Idefix.ApiTests;

public sealed class ChunkerTests
{
    private static readonly KnowledgeDocument Document = new("Recht", "OR.pdf", "OR.pdf", 1, DateTimeOffset.UnixEpoch);

    [Fact]
    public void ChunksNeverCrossPagesAndKeepLineRanges()
    {
        var parsed = new ParsedDocument([
            Page(1, "eins", "zwei"),
            Page(2, "drei"),
        ]);

        var chunks = Chunker.Chunk(Document, parsed, ChunkingOptions.Default);

        Assert.Collection(
            chunks,
            c => Assert.Equal((1, 1, 2, "eins\nzwei"), (c.Page, c.FirstLine, c.LastLine, c.Text)),
            c => Assert.Equal((2, 1, 1, "drei"), (c.Page, c.FirstLine, c.LastLine, c.Text)));
    }

    [Fact]
    public void LongPagesSplitWithOverlap()
    {
        var lines = Enumerable.Range(1, 10).Select(i => new string('x', 20) + i).ToArray();
        var parsed = new ParsedDocument([Page(1, lines)]);

        var chunks = Chunker.Chunk(Document, parsed, new ChunkingOptions(MaxCharacters: 100, OverlapLines: 1));

        Assert.True(chunks.Count > 1);
        Assert.Equal(1, chunks[0].FirstLine);
        Assert.Equal(10, chunks[^1].LastLine);
        Assert.All(chunks.Zip(chunks.Skip(1)), pair => Assert.Equal(pair.First.LastLine, pair.Second.FirstLine));
        Assert.Equal(chunks.Count, chunks.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void BlankLinesAreSkipped()
    {
        var chunks = Chunker.Chunk(Document, new ParsedDocument([Page(1, " ", string.Empty)]), ChunkingOptions.Default);

        Assert.Empty(chunks);
    }

    private static DocumentPage Page(int number, params string[] lines) =>
        new(number, lines.Select((text, i) => new TextLine(i + 1, text, Bounds: null)).ToList());
}
