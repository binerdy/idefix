using Idefix.Core.Agent;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;
using Idefix.Core.Markdown;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Agent;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Llm;
using Idefix.Infrastructure.Markdown;
using Idefix.Infrastructure.Parsing;
using Idefix.Infrastructure.Search;

namespace Idefix.Infrastructure;

public sealed class IdefixRuntime : IDisposable
{
    private readonly KnowledgeWatcher watcher;
    private readonly LuceneSectionSearch search;

    private IdefixRuntime(
        IdefixSettings settings,
        IKnowledgeCatalog catalog,
        IDocumentParser parser,
        IDocumentLibrary library,
        ISectionStore sections,
        LuceneSectionSearch search,
        IAnswerEngine answerEngine,
        KnowledgeWatcher watcher)
    {
        this.Settings = settings;
        this.Catalog = catalog;
        this.Parser = parser;
        this.Library = library;
        this.Sections = sections;
        this.AnswerEngine = answerEngine;
        this.search = search;
        this.watcher = watcher;
        this.watcher.Changed += (_, _) => this.KnowledgeChanged?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? KnowledgeChanged;

    public IdefixSettings Settings { get; }

    public IKnowledgeCatalog Catalog { get; }

    public IDocumentParser Parser { get; }

    public IDocumentLibrary Library { get; }

    public ISectionStore Sections { get; }

    public IAnswerEngine AnswerEngine { get; }

    public static IdefixRuntime Create(IdefixSettings settings, HttpClient httpClient, TimeProvider timeProvider)
    {
        Directory.CreateDirectory(settings.RootDirectory);
        var catalog = new FileSystemKnowledgeCatalog();
        var parser = CompositeDocumentParser.CreateDefault();
        var library = new DocumentLibrary(settings.RootDirectory, catalog, parser);
        var sections = new MarkdownSectionStore();
        var search = new LuceneSectionSearch();
        var engine = new SemanticKernelAnswerEngine(
            LlmClients.CreateKernel(settings.Llm, httpClient),
            library,
            sections,
            search,
            new AnswerEngineOptions(settings.PrePrompt, settings.Llm.MaxContextTokens),
            timeProvider);
        var watcher = new KnowledgeWatcher(settings.RootDirectory, TimeSpan.FromSeconds(2), timeProvider);
        return new IdefixRuntime(settings, catalog, parser, library, sections, search, engine, watcher);
    }

    public IReadOnlyList<KnowledgePlugin> Plugins() => this.Catalog.DiscoverPlugins(this.Settings.RootDirectory);

    public string ResolveDocumentPath(string documentKey) =>
        Path.Combine(this.Settings.RootDirectory, documentKey.Replace('/', Path.DirectorySeparatorChar));

    public void Dispose()
    {
        this.watcher.Dispose();
        this.search.Dispose();
        (this.Sections as IDisposable)?.Dispose();
    }
}
