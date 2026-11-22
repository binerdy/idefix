namespace Idefix.Core.Settings;

public interface ISettingsStore
{
    Task<IdefixSettings?> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(IdefixSettings settings, CancellationToken cancellationToken);
}
