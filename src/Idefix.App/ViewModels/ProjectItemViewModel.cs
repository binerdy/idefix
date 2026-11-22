using System.Collections.ObjectModel;
using Idefix.Core.Chat;

namespace Idefix.App.ViewModels;

public sealed partial class ProjectItemViewModel(Project project) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Name))]
    public partial Project Project { get; set; } = project;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; } = true;

    public string Name => this.Project.Name;

    public ObservableCollection<SessionItemViewModel> Sessions { get; } = [];

    [RelayCommand]
    public void Toggle() => this.IsExpanded = !this.IsExpanded;
}
