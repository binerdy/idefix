using System.ClientModel;
using System.Net;
using System.Text;
using System.Threading.Channels;
using Idefix.Core.Agent;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace Idefix.Infrastructure.Agent;

internal sealed class AnswerConversation(Kernel kernel, ChatHistory history, ChannelWriter<AnswerEvent> writer)
{
    private readonly IChatCompletionService chat = kernel.GetRequiredService<IChatCompletionService>();

    public async Task<string> AnswerAsync(CancellationToken cancellationToken) =>
        (await this.StreamAsync(settings: null, cancellationToken)).Text;

    public async Task<string> AnswerAsync(SectionCollector collector, SectionTools tools, int maxRounds, CancellationToken cancellationToken)
    {
        // Round 1 was the local search.
        for (var round = 2; round <= maxRounds; round++)
        {
            string text;
            IReadOnlyList<FunctionCallContent> calls;
            try
            {
                (text, calls) = await this.StreamAsync(Offer(FunctionChoiceBehavior.Auto(tools.Functions, autoInvoke: false)), cancellationToken);
            }
            catch (Exception ex) when (round == 2 && IsBadRequest(ex))
            {
                // Some local models reject any request that offers tools; they answer from the local search.
                return await this.AnswerAsync(cancellationToken);
            }

            if (calls.Count == 0)
            {
                return text;
            }

            if (text.Length > 0)
            {
                await writer.WriteAsync(new TextDiscarded(), cancellationToken);
            }

            await writer.WriteAsync(new SelectingSections(round, collector.Available.Count), cancellationToken);
            await this.InvokeAsync(text, calls, tools, cancellationToken);
            await this.PublishRoundAsync(round, collector, cancellationToken);
            await writer.WriteAsync(new ContextPrepared(collector.BuildContext().Summary), cancellationToken);
        }

        return (await this.StreamAsync(Offer(FunctionChoiceBehavior.None(tools.Functions)), cancellationToken)).Text;
    }

    private static OpenAIPromptExecutionSettings Offer(FunctionChoiceBehavior behavior) => new() { FunctionChoiceBehavior = behavior };

    // Semantic Kernel wraps HTTP errors of complete replies, but lets those of streamed replies through.
    private static bool IsBadRequest(Exception exception) => exception switch
    {
        HttpOperationException wrapped => wrapped.StatusCode == HttpStatusCode.BadRequest,
        ClientResultException raw => raw.Status == (int)HttpStatusCode.BadRequest,
        _ => false,
    };

    private async Task<(string Text, IReadOnlyList<FunctionCallContent> Calls)> StreamAsync(PromptExecutionSettings? settings, CancellationToken cancellationToken)
    {
        var text = new StringBuilder();
        var calls = new FunctionCallContentBuilder();
        await foreach (var update in this.chat.GetStreamingChatMessageContentsAsync(history, settings, kernel, cancellationToken))
        {
            calls.Append(update);
            if (update.Content is { Length: > 0 } content)
            {
                text.Append(content);
                await writer.WriteAsync(new TextDelta(content), cancellationToken);
            }
        }

        return (text.ToString(), calls.Build());
    }

    private async Task InvokeAsync(string text, IReadOnlyList<FunctionCallContent> calls, SectionTools tools, CancellationToken cancellationToken)
    {
        var request = new ChatMessageContent(AuthorRole.Assistant, text.Length > 0 ? text : null);
        foreach (var call in calls)
        {
            request.Items.Add(call);
        }

        history.Add(request);
        foreach (var call in calls)
        {
            var result = await tools.InvokeAsync(call, kernel, cancellationToken);
            history.Add(new FunctionResultContent(call, result).ToChatMessage());
        }
    }

    private async Task PublishRoundAsync(int number, SectionCollector collector, CancellationToken cancellationToken)
    {
        if (collector.CompleteRound(number) is { } round)
        {
            await writer.WriteAsync(new SectionsPicked(round, collector.Available.Count), cancellationToken);
        }
    }
}
