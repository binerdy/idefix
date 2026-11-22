using System.ClientModel;
using System.ClientModel.Primitives;
using Idefix.Core.Settings;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using OpenAI;

namespace Idefix.Infrastructure.Llm;

public sealed record LlmClients(Kernel Kernel, IEmbeddingGenerator<string, Embedding<float>> Embeddings)
{
    public static LlmClients Create(LlmSettings settings, HttpClient httpClient)
    {
        var client = new OpenAIClient(
            new ApiKeyCredential(string.IsNullOrWhiteSpace(settings.ApiKey) ? "not-set" : settings.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = settings.EffectiveEndpoint,
                Transport = new HttpClientPipelineTransport(httpClient),
            });

        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(settings.ChatModel, client);
        var embeddings = client.GetEmbeddingClient(settings.EmbeddingModel).AsIEmbeddingGenerator();
        return new LlmClients(builder.Build(), embeddings);
    }
}
