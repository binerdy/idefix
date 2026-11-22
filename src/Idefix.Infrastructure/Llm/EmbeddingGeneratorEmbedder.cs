using Idefix.Core.Indexing;
using Microsoft.Extensions.AI;

namespace Idefix.Infrastructure.Llm;

public sealed class EmbeddingGeneratorEmbedder(IEmbeddingGenerator<string, Embedding<float>> generator) : ITextEmbedder
{
    public async Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        var embeddings = await generator.GenerateAsync(texts, cancellationToken: cancellationToken);
        return embeddings.Select(e => e.Vector).ToList();
    }
}
