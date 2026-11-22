namespace Idefix.Core.Settings;

public interface IModelCatalog
{
    Task<IReadOnlyList<string>> ListModelsAsync(LlmSettings settings, CancellationToken cancellationToken);
}
