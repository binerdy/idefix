using Idefix.ApiTests.Support;
using Idefix.Core.Agent;
using Idefix.Infrastructure.Agent;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Markdown;
using Idefix.Infrastructure.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Idefix.ApiTests;

public sealed class AnswerEngineTests : IDisposable
{
    private readonly TempDirectory root = new();
    private readonly MarkdownSectionStore store = new();
    private readonly CancellationToken cancellation = TestContext.Current.CancellationToken;

    public AnswerEngineTests()
    {
        TestDocuments.WritePdf(this.root.Combine("Recht", "Mietrecht.pdf"), "Einleitung", "Die Kuendigungsfrist betraegt drei Monate");
        TestDocuments.WriteDocx(this.root.Combine("Kochen", "Zopf.docx"), "Zopf braucht Butter und Mehl");
    }

    [Fact]
    public async Task EveryActiveDocumentIsSentWithPageMarkers()
    {
        var chat = new ScriptedChatCompletion("Drei Monate.");

        await Collect(this.CreateEngine(chat).AskAsync(new AnswerRequest("Kuendigungsfrist?", [], ["Recht", "Kochen"]), this.cancellation));

        var prompt = chat.LastHistory![^1].Content!;
        Assert.StartsWith("Kuendigungsfrist?", prompt, StringComparison.Ordinal);
        Assert.Contains("--- [Mietrecht.pdf, p. 1] ---", prompt, StringComparison.Ordinal);
        Assert.Contains("--- [Mietrecht.pdf, p. 2] ---", prompt, StringComparison.Ordinal);
        Assert.Contains("Zopf braucht Butter und Mehl", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DisabledPluginsAreNotSent()
    {
        var chat = new ScriptedChatCompletion("ok");

        var events = await Collect(this.CreateEngine(chat).AskAsync(new AnswerRequest("Frage", [], ["Recht"]), this.cancellation));

        Assert.DoesNotContain("Zopf", chat.LastHistory![^1].Content, StringComparison.Ordinal);
        Assert.All(Assert.Single(events.OfType<ContextPrepared>()).Summary.Documents, label => Assert.StartsWith("Mietrecht.pdf ›", label, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnswerStreamsAndCitationsPointToTheCitedLines()
    {
        var chat = new ScriptedChatCompletion("Die Kuendigungsfrist betraegt drei Monate [Mietrecht.pdf, p. 2].");

        var events = await Collect(this.CreateEngine(chat).AskAsync(new AnswerRequest("Frist?", [], ["Recht"]), this.cancellation));

        Assert.IsType<ContextPrepared>(events[0]);
        var completed = Assert.IsType<AnswerCompleted>(events[^1]);
        Assert.Equal(string.Concat(events.OfType<TextDelta>().Select(d => d.Text)), completed.Message.Content);
        Assert.NotNull(completed.Message.Context);
        var citation = Assert.Single(completed.Message.Citations);
        Assert.Equal(("Recht/Mietrecht.pdf", 2, 1, 1), (citation.DocumentKey, citation.Page, citation.FirstLine, citation.LastLine));
    }

    [Fact]
    public async Task DocumentsBeyondTheContextWindowAreRejectedBeforeCallingTheModel()
    {
        var chat = new ScriptedChatCompletion("never");
        var engine = this.CreateEngine(chat, maxContextTokens: 10);

        var failure = await Assert.ThrowsAsync<ContextTooLargeException>(() =>
            Collect(engine.AskAsync(new AnswerRequest("Frage", [], ["Recht", "Kochen"]), this.cancellation)));

        Assert.Equal(10, failure.Limit);
        Assert.True(failure.EstimatedTokens > 10);
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task HistoryAndPrePromptAreIncluded()
    {
        var chat = new ScriptedChatCompletion("ok");
        var history = new[]
        {
            Idefix.Core.Chat.ChatMessage.FromUser("Erste Frage", TimeProvider.System),
            new Idefix.Core.Chat.ChatMessage(Idefix.Core.Chat.ChatRole.Assistant, "Erste Antwort", DateTimeOffset.UtcNow, null, []),
        };

        await Collect(this.CreateEngine(chat).AskAsync(new AnswerRequest("Zweite Frage", history, ["Recht"]), this.cancellation));

        var messages = chat.LastHistory!;
        Assert.Contains("Antworte knapp.", messages[0].Content, StringComparison.Ordinal);
        Assert.Equal(["Erste Frage", "Erste Antwort"], messages.Skip(1).Take(2).Select(m => m.Content));
    }

    [Fact]
    public async Task SmallLibrariesAreSentWithoutASelectionStep()
    {
        var chat = new ScriptedChatCompletion("ok");

        var events = await Collect(this.CreateEngine(chat).AskAsync(new AnswerRequest("Frage", [], ["Recht", "Kochen"]), this.cancellation));

        Assert.Equal(0, chat.SelectionCalls);
        Assert.DoesNotContain(events, e => e is SelectingSections);
        var summary = Assert.Single(events.OfType<ContextPrepared>()).Summary;
        Assert.Equal(summary.TotalSections, summary.Documents.Count);
    }

    [Fact]
    public async Task LargerLibrariesSendOnlyTheSectionsTheModelPicks()
    {
        var chat = new ScriptedChatCompletion("Drei Monate [Mietrecht.pdf, p. 2].", "{\"sections\": [\"S2\"]}");

        var events = await Collect(this.CreateEngine(chat, selectionThreshold: 1).AskAsync(new AnswerRequest("Kuendigungsfrist?", [], ["Recht", "Kochen"]), this.cancellation));

        Assert.Equal(2, chat.SelectionCalls);
        Assert.Contains("S1 | Zopf.docx", chat.SelectionPrompt, StringComparison.Ordinal);
        Assert.Contains("Question: Kuendigungsfrist?", chat.SelectionPrompt, StringComparison.Ordinal);
        var selecting = events.OfType<SelectingSections>().First();
        var summary = Assert.Single(events.OfType<ContextPrepared>()).Summary;
        Assert.True(summary.TotalSections >= 2);
        Assert.Equal(selecting.TotalSections, summary.TotalSections);
        Assert.Single(summary.Documents);
        var prompt = chat.LastHistory![^1].Content!;
        Assert.DoesNotContain("Zopf", prompt, StringComparison.Ordinal);
        Assert.Contains("[Mietrecht.pdf, p.", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PingPongAddsSectionsOverSeveralRoundsAndShowsThemLive()
    {
        TestDocuments.WriteDocx(this.root.Combine("Recht", "Vertrag.docx"), "# Kuendigung\nDrei Monate\n# Kaution\nZwei Mieten");
        var chat = new ScriptedChatCompletion("ok", "{\"sections\": [\"S2\"]}", "{\"sections\": [\"S3\"]}", "{\"sections\": []}");

        var events = await Collect(this.CreateEngine(chat, selectionThreshold: 1).AskAsync(new AnswerRequest("Frage", [], ["Recht", "Kochen"]), this.cancellation));

        Assert.Equal(3, chat.SelectionCalls);
        Assert.Contains("Sections you already have", chat.SelectionPrompts[1], StringComparison.Ordinal);
        Assert.Contains("Mietrecht.pdf", chat.SelectionPrompts[1], StringComparison.Ordinal);
        Assert.Equal([1, 2], events.OfType<SectionsPicked>().Select(p => p.Round.Number));
        Assert.Equal([1, 2, 3], events.OfType<SelectingSections>().Select(s => s.Round));
        var summary = Assert.Single(events.OfType<ContextPrepared>()).Summary;
        Assert.Equal(2, summary.Documents.Count);
        Assert.Equal(2, summary.Rounds!.Count);
    }

    [Fact]
    public async Task WithoutNachdenkenThereIsOnlyOneRound()
    {
        var chat = new ScriptedChatCompletion("ok", "{\"sections\": [\"S2\"]}", "{\"sections\": [\"S3\"]}");

        var events = await Collect(this.CreateEngine(chat, selectionThreshold: 1).AskAsync(new AnswerRequest("Frage", [], ["Recht", "Kochen"], DeepThinking: false), this.cancellation));

        Assert.Equal(1, chat.SelectionCalls);
        Assert.Single(Assert.Single(events.OfType<ContextPrepared>()).Summary.Documents);
    }

    [Fact]
    public async Task AnUnreadableSelectionFallsBackToEverySection()
    {
        var chat = new ScriptedChatCompletion("ok", "the first one, I guess");

        var events = await Collect(this.CreateEngine(chat, selectionThreshold: 1).AskAsync(new AnswerRequest("Frage", [], ["Recht", "Kochen"]), this.cancellation));

        var summary = Assert.Single(events.OfType<ContextPrepared>()).Summary;
        Assert.Equal(summary.TotalSections, summary.Documents.Count);
    }

    [Fact]
    public async Task MarkdownSectionsAreWrittenNextToThePlugins()
    {
        await Collect(this.CreateEngine(new ScriptedChatCompletion("ok")).AskAsync(new AnswerRequest("Frage", [], ["Recht"]), this.cancellation));

        Assert.NotEmpty(Directory.GetFiles(this.root.Combine("Recht", "_markdown", "Mietrecht.pdf"), "*.md"));
        Assert.False(Directory.Exists(this.root.Combine("Kochen", "_markdown")));
    }

    public void Dispose()
    {
        this.store.Dispose();
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

    private SemanticKernelAnswerEngine CreateEngine(ScriptedChatCompletion chat, int maxContextTokens = 100_000, int selectionThreshold = 8_000)
    {
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(chat);
        return new SemanticKernelAnswerEngine(
            builder.Build(),
            new DocumentLibrary(this.root.Path, new FileSystemKnowledgeCatalog(), CompositeDocumentParser.CreateDefault()),
            this.store,
            new AnswerEngineOptions("Antworte knapp.", maxContextTokens, SelectionThresholdTokens: selectionThreshold),
            TimeProvider.System);
    }
}
