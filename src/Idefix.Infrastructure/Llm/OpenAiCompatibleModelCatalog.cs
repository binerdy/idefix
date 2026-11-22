using System.Net.Http.Headers;
using Idefix.Core.Settings;
using Idefix.Infrastructure.Persistence;

namespace Idefix.Infrastructure.Llm;

public sealed class OpenAiCompatibleModelCatalog(HttpClient httpClient) : IModelCatalog
{
    public async Task<IReadOnlyList<string>> ListModelsAsync(LlmSettings settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(settings.EffectiveEndpoint, "models"));
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var models = await response.Content.ReadFromJsonAsync(IdefixJsonContext.Default.ModelList, cancellationToken);
        return models?.Data.Select(m => m.Id).Order(StringComparer.OrdinalIgnoreCase).ToList() ?? [];
    }
}
