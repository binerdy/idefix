namespace Idefix.Core.Indexing;

public sealed record EmbeddedChunk(DocumentChunk Chunk, ReadOnlyMemory<float> Vector);
