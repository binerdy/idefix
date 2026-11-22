using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
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

    private static List<DocumentSection> WithinBudget(IReadOnlyList<DocumentSection> chosen, int budget)
    {
        var kept = new List<DocumentSection>();
        var used = 0;
        foreach (var section in chosen)
        {
            if (used + section.EstimatedTokens > budget)
            {
                continue;
            }

            kept.Add(section);
            used += section.EstimatedTokens;
        }

        return kept;
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
        var (chosen, rounds) = await this.ChooseAsync(request, available, writer, cancellationToken);

        var context = DocumentContext.Build(chosen, available.Count, rounds.Count > 0 ? rounds : null);
        if (context.Summary.EstimatedTokens > options.MaxContextTokens)
        {
            throw new ContextTooLargeException(context.Summary.EstimatedTokens, options.MaxContextTokens);
        }

        await writer.WriteAsync(new ContextPrepared(context.Summary), cancellationToken);

        var history = new ChatHistory(PromptComposer.SystemPrompt(options.PrePrompt, request.Plugins));
        foreach (var previous in request.History.TakeLast(options.HistoryMessages))
        {
            history.AddMessage(previous.Role == ChatRole.User ? AuthorRole.User : AuthorRole.Assistant, previous.Content);
        }

        history.AddUserMessage(PromptComposer.Question(request.Question, context));

        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var answer = new StringBuilder();
        var updates = chat.GetStreamingChatMessageContentsAsync(history, executionSettings: null, kernel, cancellationToken)
            .Select(u => u.Content)
            .OfType<string>()
            .Where(content => content.Length > 0);
        await foreach (var content in updates)
        {
            answer.Append(content);
            await writer.WriteAsync(new TextDelta(content), cancellationToken);
        }

        var text = answer.ToString();
        var message = new ChatMessage(ChatRole.Assistant, text, timeProvider.GetUtcNow(), context.Summary, CitationLocator.Resolve(text, snapshot.Documents));
        await writer.WriteAsync(new AnswerCompleted(message), cancellationToken);
    }

    private async Task<(List<DocumentSection> Chosen, List<SelectionRound> Rounds)> ChooseAsync(
        AnswerRequest request,
        IReadOnlyList<DocumentSection> available,
        ChannelWriter<AnswerEvent> writer,
        CancellationToken cancellationToken)
    {
        var total = available.Sum(s => s.EstimatedTokens);
        if (available.Count <= 1 || total <= options.SelectionThresholdTokens)
        {
            return ([.. available], []);
        }

        var budget = options.MaxContextTokens * 3 / 4;
        var outline = SectionOutline.Build(available);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var chosen = new List<DocumentSection>();
        var rounds = new List<SelectionRound>();
        var maxRounds = request.DeepThinking ? options.MaxRounds : 1;

        for (var round = 1; round <= maxRounds; round++)
        {
            await writer.WriteAsync(new SelectingSections(round, available.Count), cancellationToken);
            var used = chosen.Sum(s => s.EstimatedTokens);
            var prompt = round == 1
                ? PromptComposer.SelectionPrompt(request.Question, outline, budget)
                : PromptComposer.FollowUpPrompt(request.Question, outline, DocumentContext.Build(chosen, available.Count), budget - used);
            if (DocumentContext.EstimateTokens(prompt) > options.MaxContextTokens)
            {
                if (round == 1)
                {
                    throw new ContextTooLargeException(DocumentContext.EstimateTokens(prompt), options.MaxContextTokens);
                }

                break;
            }

            var history = new ChatHistory();
            history.AddUserMessage(prompt);
            var reply = await chat.GetChatMessageContentAsync(history, executionSettings: null, kernel, cancellationToken);
            var picked = SectionOutline.ParseSelection(reply.Content ?? string.Empty, available);
            var unreadable = picked is null;
            if (unreadable && round > 1)
            {
                break;
            }

            var added = WithinBudget((picked ?? available).Except(chosen).ToList(), budget - used);
            if (added.Count == 0)
            {
                break;
            }

            chosen.AddRange(added);
            var selection = new SelectionRound(round, added.Select(s => s.Label).ToList());
            rounds.Add(selection);
            await writer.WriteAsync(new SectionsPicked(selection, available.Count), cancellationToken);
            if (unreadable)
            {
                break;
            }
        }

        var order = available.Select((section, index) => (section, index)).ToDictionary(p => p.section, p => p.index);
        return ([.. chosen.OrderBy(s => order[s])], rounds);
    }
}
