using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;

namespace Idefix.Infrastructure.Knowledge;

public sealed class DocumentLibrary(string rootDirectory, IKnowledgeCatalog catalog, IDocumentParser parser) : IDocumentLibrary
{
    private readonly ConcurrentDictionary<string, (DocumentFingerprint Fingerprint, ParsedDocument Content)> cache = new(StringComparer.Ordinal);

    public int Parses { get; private set; }

    public async Task<LibrarySnapshot> LoadAsync(IReadOnlyCollection<string>? plugins, IProgress<LibraryProgress>? progress, CancellationToken cancellationToken)
    {
        var documents = catalog.DiscoverPlugins(rootDirectory)
            .Where(p => plugins is null || plugins.Contains(p.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(catalog.ScanDocuments)
            .OrderBy(d => d.Key, StringComparer.Ordinal)
            .ToList();

        this.Forget(documents, plugins);

        var loaded = new List<LoadedDocument>(documents.Count);
        var failures = new List<DocumentFailure>();
        for (var i = 0; i < documents.Count; i++)
        {
            progress?.Report(new LibraryProgress(documents.Count, i));
            var document = documents[i];
            var content = await this.ReadAsync(document, failures, cancellationToken);
            if (content is not null)
            {
                loaded.Add(new LoadedDocument(document, content));
            }
        }

        progress?.Report(new LibraryProgress(documents.Count, documents.Count));
        return new LibrarySnapshot(loaded, failures);
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "One unreadable document must not hide the others.")]
    private async Task<ParsedDocument?> ReadAsync(KnowledgeDocument document, List<DocumentFailure> failures, CancellationToken cancellationToken)
    {
        if (this.cache.TryGetValue(document.Key, out var cached) && cached.Fingerprint == document.Fingerprint)
        {
            return cached.Content;
        }

        try
        {
            var content = await Task.Run(() => parser.Parse(document.FullPath), cancellationToken);
            this.Parses++;
            this.cache[document.Key] = (document.Fingerprint, content);
            return content;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            this.cache.TryRemove(document.Key, out _);
            failures.Add(new DocumentFailure(document.Key, ex.Message));
            return null;
        }
    }

    private void Forget(IReadOnlyList<KnowledgeDocument> present, IReadOnlyCollection<string>? plugins)
    {
        if (plugins is not null)
        {
            return;
        }

        var keys = present.Select(d => d.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var stale in this.cache.Keys.Where(k => !keys.Contains(k)))
        {
            this.cache.TryRemove(stale, out _);
        }
    }
}
