using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Idefix.ApiTests.Support;

internal sealed class ScriptedChatCompletion(string answer, params string[] toolQueries) : IChatCompletionService
{
    public ChatHistory? LastHistory { get; private set; }

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var parts = new List<string>();
        await foreach (var update in this.GetStreamingChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken))
        {
            parts.Add(update.Content ?? string.Empty);
        }

        return [new ChatMessageContent(AuthorRole.Assistant, string.Concat(parts))];
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.LastHistory = chatHistory;
        if (kernel is not null && kernel.Plugins.TryGetFunction("knowledge", "search_plugins", out var search))
        {
            foreach (var query in toolQueries)
            {
                await kernel.InvokeAsync(search, new KernelArguments { ["query"] = query }, cancellationToken);
            }
        }

        foreach (var word in answer.Split(' '))
        {
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, word + " ");
        }
    }
}
