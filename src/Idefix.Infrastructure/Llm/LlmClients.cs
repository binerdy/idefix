using System.ClientModel;
using System.ClientModel.Primitives;
using Idefix.Core.Settings;
using Microsoft.SemanticKernel;
using OpenAI;

namespace Idefix.Infrastructure.Llm;

public static class LlmClients
{
    // Shared or CPU-bound servers can queue a long prompt for minutes before the first byte arrives.
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    public static Kernel CreateKernel(LlmSettings settings, HttpClient httpClient)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(settings.ApiKey) ? "not-set" : settings.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = settings.EffectiveEndpoint,
                Transport = new HttpClientPipelineTransport(httpClient),
                NetworkTimeout = RequestTimeout,
                RetryPolicy = new ClientRetryPolicy(maxRetries: 1),
            });

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(settings.ChatModel, client);
        return builder.Build();
    }
}
