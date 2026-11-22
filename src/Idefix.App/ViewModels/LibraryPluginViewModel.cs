using Idefix.App.Services;

namespace Idefix.App.ViewModels;

public sealed partial class LibraryPluginViewModel(string name, string path, int documentCount, bool isEnabled, Action<LibraryPluginViewModel> toggled) : ObservableObject
{
    public string Name { get; } = name;

    public string Path { get; } = path;

    public string Documents { get; } = Strings.Format("DocumentsFormat", documentCount);

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = isEnabled;

    [RelayCommand]
    public void OpenFolder() => LibraryViewModel.OpenFolder(this.Path);

    partial void OnIsEnabledChanged(bool value) => toggled(this);
}
