using Idefix.Core.Agent;
using Idefix.Core.Documents;
using Idefix.Core.Indexing;
using Idefix.Core.Knowledge;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Agent;
using Idefix.Infrastructure.Indexing;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Llm;
using Idefix.Infrastructure.Parsing;

namespace Idefix.Infrastructure;

public sealed class IdefixRuntime : IDisposable
{
    private readonly KnowledgeWatcher watcher;

    private IdefixRuntime(
        IdefixSettings settings,
        IKnowledgeCatalog catalog,
        IDocumentParser parser,
        IVectorIndex index,
        IKnowledgeIndexer indexer,
        IAnswerEngine answerEngine,
        KnowledgeWatcher watcher)
    {
        this.Settings = settings;
        this.Catalog = catalog;
        this.Parser = parser;
        this.Index = index;
        this.Indexer = indexer;
        this.AnswerEngine = answerEngine;
        this.watcher = watcher;
        this.watcher.Changed += (_, _) => this.KnowledgeChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? KnowledgeChanged;

    public IdefixSettings Settings { get; }

    public IKnowledgeCatalog Catalog { get; }

    public IDocumentParser Parser { get; }

    public IVectorIndex Index { get; }

    public IKnowledgeIndexer Indexer { get; }

    public IAnswerEngine AnswerEngine { get; }

    public static IdefixRuntime Create(IdefixSettings settings, HttpClient httpClient, TimeProvider timeProvider)
    {
        Directory.CreateDirectory(settings.RootDirectory);
        var catalog = new FileSystemKnowledgeCatalog();
        var parser = CompositeDocumentParser.CreateDefault();
        var index = new SqliteVectorIndex(settings.RootDirectory, settings.Llm.EmbeddingModel);
        var clients = LlmClients.Create(settings.Llm, httpClient);
        var embedder = new EmbeddingGeneratorEmbedder(clients.Embeddings);
        var indexer = new KnowledgeIndexer(catalog, parser, embedder, index, ChunkingOptions.Default);
        var search = new KnowledgeSearch(embedder, index);
        var engine = new SemanticKernelAnswerEngine(
            clients.Kernel,
            search,
            parser,
            new AnswerEngineOptions(settings.RootDirectory, settings.PrePrompt, settings.ContextMode),
            timeProvider);
        var watcher = new KnowledgeWatcher(settings.RootDirectory, TimeSpan.FromSeconds(2), timeProvider);
        return new IdefixRuntime(settings, catalog, parser, index, indexer, engine, watcher);
    }

    public IReadOnlyList<KnowledgePlugin> Plugins() => this.Catalog.DiscoverPlugins(this.Settings.RootDirectory);

    public string ResolveDocumentPath(string documentKey) =>
        Path.Combine(this.Settings.RootDirectory, documentKey.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        this.watcher.Dispose();
        (this.Index as IDisposable)?.Dispose();
    }
}
