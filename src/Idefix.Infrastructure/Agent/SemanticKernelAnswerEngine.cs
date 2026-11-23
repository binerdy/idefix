using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Idefix.Core.Agent;
using Idefix.Core.Chat;
using Idefix.Core.Documents;
using Idefix.Core.Knowledge;
using Idefix.Core.Markdown;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ChatMessage = Idefix.Core.Chat.ChatMessage;

namespace Idefix.Infrastructure.Agent;

public sealed class SemanticKernelAnswerEngine(
    Kernel kernel,
    IDocumentLibrary library,
    ISectionStore sections,
    ISectionSearch search,
    AnswerEngineOptions options,
    TimeProvider timeProvider) : IAnswerEngine
{
    public async IAsyncEnumerable<AnswerEvent> AskAsync(AnswerRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<AnswerEvent>(new UnboundedChannelOptions { SingleReader = true });
        var run = Task.Run(() => this.RunAsync(request, channel.Writer, cancellationToken), cancellationToken);
        await foreach (var answerEvent in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return answerEvent;
        }

        await run;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any failure is forwarded to the reader through the channel.")]
    private async Task RunAsync(AnswerRequest request, ChannelWriter<AnswerEvent> writer, CancellationToken cancellationToken)
    {
        try
        {
            await this.AnswerAsync(request, writer, cancellationToken);
            writer.TryComplete();
        }
        catch (Exception ex)
        {
            writer.TryComplete(ex);
        }
    }

    private async Task AnswerAsync(AnswerRequest request, ChannelWriter<AnswerEvent> writer, CancellationToken cancellationToken)
    {
        var snapshot = await library.LoadAsync(request.Plugins, progress: null, cancellationToken);
        var available = await sections.SynchronizeAsync(snapshot.Documents, cancellationToken);
        var sendAll = available.Count <= 1 || available.Sum(s => s.EstimatedTokens) <= options.SelectionThresholdTokens;
        var collector = new SectionCollector(available, sendAll ? int.MaxValue : options.MaxContextTokens * 3 / 4);
        if (sendAll)
        {
            collector.Add(available);
        }
        else
        {
            await this.ChooseAsync(request.Question, collector, writer, cancellationToken);
        }

        var context = collector.BuildContext();
        if (context.Summary.EstimatedTokens > options.MaxContextTokens)
        {
            throw new ContextTooLargeException(context.Summary.EstimatedTokens, options.MaxContextTokens);
        }

        await writer.WriteAsync(new ContextPrepared(context.Summary), cancellationToken);

        var mayRequestMore = !sendAll && request.DeepThinking;
        var conversation = new AnswerConversation(kernel, this.History(request, context, mayRequestMore), writer);
        var text = mayRequestMore
            ? await conversation.AnswerAsync(collector, new SectionTools(collector, search, options.SearchLimit), options.MaxRounds, cancellationToken)
            : await conversation.AnswerAsync(cancellationToken);

        var message = new ChatMessage(ChatRole.Assistant, text, timeProvider.GetUtcNow(), collector.BuildContext().Summary, CitationLocator.Resolve(text, snapshot.Documents));
        await writer.WriteAsync(new AnswerCompleted(message), cancellationToken);
    }

    private async Task ChooseAsync(string question, SectionCollector collector, ChannelWriter<AnswerEvent> writer, CancellationToken cancellationToken)
    {
        await writer.WriteAsync(new SelectingSections(1, collector.Available.Count), cancellationToken);
        collector.Add(await new SectionChooser(kernel, search, options).ChooseAsync(question, collector, cancellationToken));
        if (collector.CompleteRound(1) is { } round)
        {
            await writer.WriteAsync(new SectionsPicked(round, collector.Available.Count), cancellationToken);
        }
    }

    private ChatHistory History(AnswerRequest request, DocumentContext context, bool mayRequestMore)
    {
        var history = new ChatHistory(PromptComposer.SystemPrompt(options.PrePrompt, request.Plugins, mayRequestMore));
        foreach (var previous in request.History.TakeLast(options.HistoryMessages))
        {
            history.AddMessage(previous.Role == ChatRole.User ? AuthorRole.User : AuthorRole.Assistant, previous.Content);
        }

        history.AddUserMessage(PromptComposer.Question(request.Question, context));
        return history;
    }
}
