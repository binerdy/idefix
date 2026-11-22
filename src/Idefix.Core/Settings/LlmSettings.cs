using System.Diagnostics.CodeAnalysis;

namespace Idefix.Core.Settings;

public sealed record LlmSettings(
    LlmProviderKind Provider,
    Uri? Endpoint,
    string ApiKey,
    string ChatModel,
    string EmbeddingModel)
{
    public static LlmSettings Default { get; } = new(LlmProviderKind.Ollama, null, string.Empty, string.Empty, "nomic-embed-text");

    public Uri EffectiveEndpoint => this.Endpoint ?? DefaultEndpoint(this.Provider);

    [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Well-known provider defaults; overridable per profile.")]
    public static Uri DefaultEndpoint(LlmProviderKind provider) => provider switch
    {
        LlmProviderKind.OpenAI => new Uri("https://api.openai.com/v1/"),
        _ => new Uri("http://localhost:11434/v1/"),
    };

    [SuppressMessage("Major Code Smell", "S1075:URIs should not be hardcoded", Justification = "Relative URIs resolve against the last segment only with a trailing slash.")]
    public static Uri AsBaseAddress(Uri endpoint) =>
        endpoint.AbsoluteUri[^1] == '/' ? endpoint : new Uri(endpoint.AbsoluteUri + "/");
}
