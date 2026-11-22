using System.Net;
using System.Text;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Llm;

namespace Idefix.ApiTests;

public sealed class ModelCatalogTests
{
    [Fact]
    public async Task OllamaModelsAreDiscoveredFromTheOpenAiCompatibleEndpoint()
    {
        using var handler = new RecordingHandler("""{"object":"list","data":[{"id":"qwen3:8b"},{"id":"llama3.3"}]}""");
        using var client = new HttpClient(handler);
        var catalog = new OpenAiCompatibleModelCatalog(client);

        var models = await catalog.ListModelsAsync(LlmSettings.Default with { ApiKey = "local" }, TestContext.Current.CancellationToken);

        Assert.Equal(["llama3.3", "qwen3:8b"], models);
        Assert.Equal(new Uri("http://localhost:11434/v1/models"), handler.Request!.RequestUri);
        Assert.Equal("Bearer local", handler.Request.Headers.Authorization!.ToString());
    }

    [Fact]
    public void OllamaRequiresAnApiKeyToFunction()
    {
        var settings = IdefixSettings.CreateDefault("C:/Users/test", "Anna") with
        {
            Llm = LlmSettings.Default with { ChatModel = "llama3.3" },
        };

        Assert.Contains(SettingsIssue.ApiKeyMissing, SettingsValidator.Validate(settings));
        Assert.Empty(SettingsValidator.Validate(settings with { Llm = settings.Llm with { ApiKey = "x" } }));
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.Request = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
