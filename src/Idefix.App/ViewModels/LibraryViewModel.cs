using System.Collections.ObjectModel;
using System.Diagnostics;
using Idefix.App.Services;

namespace Idefix.App.ViewModels;

public sealed partial class LibraryViewModel(AppServices services) : ObservableObject
{
    public ObservableCollection<LibraryPluginViewModel> Plugins { get; } = [];

    public AppServices Services => services;

    public string RootDirectory => services.Settings.RootDirectory;

    public bool HasNoPlugins => this.Plugins.Count == 0;

    public static void OpenFolder(string path)
    {
        Directory.CreateDirectory(path);
        using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
    }

    public void Refresh()
    {
        this.Plugins.Clear();
        foreach (var plugin in services.Catalog.DiscoverPlugins(services.Settings.RootDirectory))
        {
            var enabled = !services.Settings.DisabledPlugins.Contains(plugin.Name, StringComparer.OrdinalIgnoreCase);
            var count = services.Catalog.ScanDocuments(plugin).Count;
            this.Plugins.Add(new LibraryPluginViewModel(plugin.Name, plugin.DirectoryPath, count, enabled, this.OnToggled));
        }

        this.OnPropertyChanged(nameof(this.RootDirectory));
        this.OnPropertyChanged(nameof(this.HasNoPlugins));
    }

    [RelayCommand]
    public void Reindex() => services.RequestLibraryRead();

    public Task RebuildMarkdownAsync(string? plugin) => services.RebuildMarkdownAsync(plugin);

    [RelayCommand]
    public void OpenRoot() => OpenFolder(services.Settings.RootDirectory);

    private void OnToggled(LibraryPluginViewModel plugin)
    {
        var disabled = services.Settings.DisabledPlugins.Where(p => !string.Equals(p, plugin.Name, StringComparison.OrdinalIgnoreCase));
        _ = services.UpdateSettingsAsync(services.Settings with
        {
            DisabledPlugins = (plugin.IsEnabled ? disabled : disabled.Append(plugin.Name)).ToList(),
        });
    }
}
