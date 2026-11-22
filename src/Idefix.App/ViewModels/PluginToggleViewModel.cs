namespace Idefix.App.ViewModels;

public sealed partial class PluginToggleViewModel(string name, bool isEnabled, Action<PluginToggleViewModel> changed) : ObservableObject
{
    public string Name { get; } = name;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; } = isEnabled;

    partial void OnIsEnabledChanged(bool value) => changed(this);
}
