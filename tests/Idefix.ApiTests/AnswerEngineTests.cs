using Idefix.ApiTests.Support;
using Idefix.Core.Agent;
using Idefix.Core.Indexing;
using Idefix.Infrastructure.Agent;
using Idefix.Infrastructure.Indexing;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Idefix.ApiTests;

public sealed class AnswerEngineTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly HashingEmbedder embedder = new();
    private readonly SqliteVectorIndex index;
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    public AnswerEngineTests()
    {
        this.index = new SqliteVectorIndex(this.root.Path, "hashing");
        TestDocuments.WritePdf(this.root.Combine("Recht", "Mietrecht.pdf"), "Einleitung", "Die Kuendigungsfrist betraegt drei Monate");
    }

    [Fact]
    public async Task FastLookupRetrievesOnceThenStreamsACitedAnswer()
    {
        var chat = new ScriptedChatCompletion("Drei Monate [Mietrecht.pdf, p. 2].");
        var engine = await this.CreateEngineAsync(chat, ContextMode.Chunks);

        var events = await Collect(engine.AskAsync(new AnswerRequest("Kuendigungsfrist?", [], ["Recht"], DeepThinking: false), this.cancellation));

        var trace = Assert.Single(events.OfType<TraceUpdated>()).Step;
        Assert.Equal(("Kuendigungsfrist?", 1), (trace.Query, trace.Round));
        Assert.Contains("Mietrecht.pdf, p. 2", trace.Sources);
        Assert.Contains("[Mietrecht.pdf, p. 2]", chat.LastHistory![^1].Content, StringComparison.Ordinal);
        var completed = Assert.IsType<AnswerCompleted>(events[^1]);
        Assert.Equal(string.Concat(events.OfType<TextDelta>().Select(d => d.Text)), completed.Message.Content);
        var citation = Assert.Single(completed.Message.Citations);
        Assert.Equal(("Recht/Mietrecht.pdf", 2, 1), (citation.DocumentKey, citation.Page, citation.FirstLine));
    }

    [Fact]
    public async Task DeepThinkingLetsTheModelSearchRepeatedlyUpToTheLimit()
    {
        var chat = new ScriptedChatCompletion("Nichts gefunden.", "erste", "zweite", "dritte", "vierte", "fuenfte");
        var engine = await this.CreateEngineAsync(chat, ContextMode.Chunks);

        var events = await Collect(engine.AskAsync(new AnswerRequest("Frage", [], ["Recht"], DeepThinking: true), this.cancellation));

        Assert.Equal(["erste", "zweite", "dritte", "vierte"], events.OfType<TraceUpdated>().Select(t => t.Step.Query));
        Assert.Contains("search", chat.LastHistory![0].Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FullFileModeSendsWholeDocumentsWithPageMarkers()
    {
        var chat = new ScriptedChatCompletion("ok");
        var engine = await this.CreateEngineAsync(chat, ContextMode.FullFiles);

        await Collect(engine.AskAsync(new AnswerRequest("Kuendigungsfrist", [], ["Recht"], DeepThinking: false), this.cancellation));

        var prompt = chat.LastHistory![^1].Content!;
        Assert.Contains("[Mietrecht.pdf, p. 1]", prompt, StringComparison.Ordinal);
        Assert.Contains("Einleitung", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailedSearchesAreTracedAndReportedToTheModel()
    {
        var chat = new ScriptedChatCompletion("Die Suche ist fehlgeschlagen.");
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chat);
        var engine = new SemanticKernelAnswerEngine(
            builder.Build(),
            new FailingSearch(),
            CompositeDocumentParser.CreateDefault(),
            new AnswerEngineOptions(this.root.Path, string.Empty, ContextMode.Chunks),
            TimeProvider.System);

        var events = await Collect(engine.AskAsync(new AnswerRequest("Frage", [], ["Recht"], DeepThinking: false), this.cancellation));

        var step = Assert.Single(events.OfType<TraceUpdated>()).Step;
        Assert.Equal("model 'nomic-embed-text' not found", step.Error);
        Assert.Contains("search failed", chat.LastHistory![^1].Content, StringComparison.OrdinalIgnoreCase);
        Assert.IsType<AnswerCompleted>(events[^1]);
    }

    public void Dispose()
    {
        this.index.Dispose();
        this.root.Dispose();
    }

    private static async Task<List<AnswerEvent>> Collect(IAsyncEnumerable<AnswerEvent> events)
    {
        var list = new List<AnswerEvent>();
        await foreach (var answerEvent in events)
        {
            list.Add(answerEvent);
        }

        return list;
    }

    private async Task<SemanticKernelAnswerEngine> CreateEngineAsync(ScriptedChatCompletion chat, ContextMode mode)
    {
        var parser = CompositeDocumentParser.CreateDefault();
        await new KnowledgeIndexer(new FileSystemKnowledgeCatalog(), parser, this.embedder, this.index, ChunkingOptions.Default)
            .SynchronizeAsync(this.root.Path, progress: null, this.cancellation);

        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chat);
        return new SemanticKernelAnswerEngine(
            builder.Build(),
            new KnowledgeSearch(this.embedder, this.index),
            parser,
            new AnswerEngineOptions(this.root.Path, "Antworte knapp.", mode),
            TimeProvider.System);
    }
}
