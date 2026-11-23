using System.Runtime.CompilerServices;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Idefix.ApiTests.Support;

internal sealed class ScriptedChatCompletion(string answer, params string[] selections) : IChatCompletionService
{
    private readonly Queue<string> pending = new(selections);

    public ChatHistory? LastHistory { get; private set; }

    public string? SelectionPrompt { get; private set; }

    public List<string> SelectionPrompts { get; } = [];

    public int Calls { get; private set; }

    public Queue<(string Function, string Arguments)> ToolCalls { get; } = new();

    public bool AlwaysCallsTools { get; init; }

    public string TextBeforeToolCalls { get; init; } = string.Empty;

    public List<FunctionChoiceBehavior?> ToolChoices { get; } = [];

    public int SelectionCalls { get; private set; }

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        this.SelectionCalls++;
        this.SelectionPrompt ??= chatHistory[^1].Content;
        this.SelectionPrompts.Add(chatHistory[^1].Content ?? string.Empty);
        var text = "no json here";
        if (this.pending.TryDequeue(out var next))
        {
            text = next;
        }
        else if (selections.Length > 0)
        {
            text = "{\"sections\": []}";
        }

        IReadOnlyList<ChatMessageContent> reply = [new ChatMessageContent(AuthorRole.Assistant, text)];
        return Task.FromResult(reply);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        this.LastHistory = chatHistory;
        this.Calls++;
        var choice = executionSettings?.FunctionChoiceBehavior;
        this.ToolChoices.Add(choice);
        if (choice is AutoFunctionChoiceBehavior && this.ToolCalls.TryPeek(out var call))
        {
            if (!this.AlwaysCallsTools)
            {
                this.ToolCalls.Dequeue();
            }

            if (this.TextBeforeToolCalls.Length > 0)
            {
                yield return new StreamingChatMessageContent(AuthorRole.Assistant, this.TextBeforeToolCalls);
            }

            var request = new StreamingChatMessageContent(AuthorRole.Assistant, content: null);
            request.Items.Add(new StreamingFunctionCallUpdateContent($"call{this.Calls}", call.Function, call.Arguments));
            yield return request;
            yield break;
        }

        foreach (var word in answer.Split(' '))
        {
            await Task.Yield();
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, word + " ");
        }
    }
}
