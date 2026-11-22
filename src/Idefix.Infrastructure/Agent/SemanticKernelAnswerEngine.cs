using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Channels;
using Idefix.Core.Agent;
using Idefix.Core.Chat;
using Idefix.Core.Documents;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ChatMessage = Idefix.Core.Chat.ChatMessage;

namespace Idefix.Infrastructure.Agent;

public sealed class SemanticKernelAnswerEngine(
    Kernel kernel,
    IKnowledgeSearch search,
    IDocumentParser parser,
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
        var retrieval = new RetrievalSession(search, parser, request.Plugins, options, step => writer.TryWrite(new TraceUpdated(step)));
        var history = new ChatHistory(PromptComposer.SystemPrompt(options.PrePrompt, request.DeepThinking, request.Plugins));
        foreach (var previous in request.History.TakeLast(options.HistoryMessages))
        {
            history.AddMessage(previous.Role == ChatRole.User ? AuthorRole.User : AuthorRole.Assistant, previous.Content);
        }

        var turnKernel = kernel.Clone();
        var settings = new OpenAIPromptExecutionSettings();
        if (request.DeepThinking)
        {
            turnKernel.Plugins.AddFromObject(retrieval, "knowledge");
            settings.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto();
            history.AddUserMessage(request.Question);
        }
        else
        {
            var passages = await retrieval.SearchPluginsAsync(request.Question, cancellationToken);
            history.AddUserMessage(string.Create(CultureInfo.InvariantCulture, $"{request.Question}\n\nRetrieved passages:\n{passages}"));
        }

        var chat = turnKernel.GetRequiredService<IChatCompletionService>();
        var answer = new StringBuilder();
        var updates = chat.GetStreamingChatMessageContentsAsync(history, settings, turnKernel, cancellationToken)
            .Select(u => u.Content)
            .OfType<string>()
            .Where(content => content.Length > 0);
        await foreach (var content in updates)
        {
            answer.Append(content);
            await writer.WriteAsync(new TextDelta(content), cancellationToken);
        }

        var text = answer.ToString();
        var message = new ChatMessage(ChatRole.Assistant, text, timeProvider.GetUtcNow(), retrieval.Trace, retrieval.ResolveCitations(text));
        await writer.WriteAsync(new AnswerCompleted(message), cancellationToken);
    }
}
