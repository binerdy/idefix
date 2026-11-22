namespace Idefix.Core.Indexing;

public interface ITextEmbedder
{
    Task<IReadOnlyList<ReadOnlyMemory<float>>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken);
}
