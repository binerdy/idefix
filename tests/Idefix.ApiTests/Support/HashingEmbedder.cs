using Idefix.Core.Indexing;

namespace Idefix.ApiTests.Support;

internal sealed class HashingEmbedder : ITextEmbedder
{
    private const int Dimensions = 128;

    private static readonly char[] Separators = [' ', '\n', '\r', '\t', '.', ',', ';', ':', '?', '!', '(', ')', '[', ']'];

    public int Calls { get; private set; }

    public Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        this.Calls++;
        IReadOnlyList<ReadOnlyMemory<float>> vectors = texts.Select(Embed).ToList();
        return Task.FromResult(vectors);
    }

    private static ReadOnlyMemory<float> Embed(string text)
    {
        var vector = new float[Dimensions];
        foreach (var token in text.ToUpperInvariant().Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            vector[Fnv(token) % Dimensions] += 1;
        }

        return vector;
    }

    private static uint Fnv(string token)
    {
        var hash = 2166136261u;
        foreach (var c in token)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
