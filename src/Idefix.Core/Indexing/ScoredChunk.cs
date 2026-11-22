namespace Idefix.Core.Indexing;

public sealed record ScoredChunk(DocumentChunk Chunk, double Score);
