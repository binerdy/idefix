using Idefix.Core.Agent;
using Idefix.Core.Markdown;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Idefix.Infrastructure.Agent;

internal sealed class SectionChooser(Kernel kernel, ISectionSearch search, AnswerEngineOptions options)
{
    public async Task<IReadOnlyList<DocumentSection>> ChooseAsync(string question, SectionCollector collector, CancellationToken cancellationToken)
    {
        var found = search.Search(question, collector.Available, options.SearchLimit);
        return found.Count > 0 ? found : await this.AskModelAsync(question, collector, cancellationToken);
    }

    private async Task<IReadOnlyList<DocumentSection>> AskModelAsync(string question, SectionCollector collector, CancellationToken cancellationToken)
    {
        var prompt = PromptComposer.SelectionPrompt(question, SectionOutline.Build(collector.Available), collector.RemainingTokens());
        var size = DocumentContext.EstimateTokens(prompt);
        if (size > options.MaxContextTokens)
        {
            throw new ContextTooLargeException(size, options.MaxContextTokens);
        }

        var history = new ChatHistory();
        history.AddUserMessage(prompt);
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var reply = await chat.GetChatMessageContentAsync(history, executionSettings: null, kernel, cancellationToken);
        return SectionOutline.ParseSelection(reply.Content ?? string.Empty, collector.Available) ?? collector.Available;
    }
}
