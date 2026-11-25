using Idefix.App.Controls;
using Idefix.App.Services;
using Idefix.App.ViewModels;
using Idefix.Core.Chat;
using Microsoft.UI.Text;
using Windows.UI.Text;

namespace Idefix.App.Views;

public sealed partial class ShellPage : Page
{
    public ShellPage(ShellViewModel viewModel)
    {
        this.ViewModel = viewModel;
        this.InitializeComponent();
        this.Root.AddHandler(PointerMovedEvent, new PointerEventHandler(this.OnPointerMoved), handledEventsToo: true);
    }

    public ShellViewModel ViewModel { get; }

    public static FontWeight WeightFor(bool selected) => selected ? FontWeights.SemiBold : FontWeights.Normal;

    private static MenuFlyoutItem MenuItem(string key, Func<Task> action) =>
        new() { Text = Strings.Get(key), Command = new AsyncRelayCommand(action) };

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e) => this.Sky.TrackPointer(e.GetCurrentPoint(this.Sky).Position);

    private void OnSessionClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: SessionItemViewModel item })
        {
            this.ViewModel.OpenSession(item);
        }
    }

    private async void OnCitationRequested(object? sender, CitationEventArgs e) => await this.ViewModel.Viewer.OpenAsync(e.Citation);

    private void OnProfileRequested(object? sender, EventArgs e) => this.ViewModel.OpenProfile();

    private void OnRestartClicked(object sender, RoutedEventArgs e) => this.ViewModel.Services.Updates.ApplyAndRestart();

    private async void OnNewProjectClicked(object sender, RoutedEventArgs e)
    {
        if (await this.PromptAsync(Strings.Get("NewProject"), string.Empty) is { } name)
        {
            await this.ViewModel.CreateProjectAsync(name);
        }
    }

    private void OnSessionRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: SessionItemViewModel item } element)
        {
            return;
        }

        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem("Rename", async () =>
        {
            if (await this.PromptAsync(Strings.Get("Rename"), item.Title) is { } title)
            {
                await this.ViewModel.UpdateSessionAsync(item.Session with { Title = title });
            }
        }));

        var move = new MenuFlyoutSubItem { Text = Strings.Get("MoveToProject") };
        move.Items.Add(MenuItem("NoProject", () => this.ViewModel.UpdateSessionAsync(item.Session with { ProjectId = null })));
        foreach (var project in this.ViewModel.AllProjects)
        {
            move.Items.Add(new MenuFlyoutItem
            {
                Text = project.Name,
                Command = new AsyncRelayCommand(() => this.ViewModel.UpdateSessionAsync(item.Session with { ProjectId = project.Id })),
            });
        }

        menu.Items.Add(move);
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Delete", () => this.ViewModel.DeleteSessionAsync(item)));
        menu.ShowAt(element, e.GetPosition(element));
    }

    private void OnProjectRightTapped(object sender, RightTappedRoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ProjectItemViewModel item } element)
        {
            return;
        }

        var menu = new MenuFlyout();
        menu.Items.Add(MenuItem("NewChatInProject", () =>
        {
            this.ViewModel.NewChatInProject(item);
            return Task.CompletedTask;
        }));
        menu.Items.Add(MenuItem("Rename", async () =>
        {
            if (await this.PromptAsync(Strings.Get("Rename"), item.Name) is { } name)
            {
                await this.ViewModel.UpdateProjectAsync(item.Project with { Name = name });
            }
        }));
        menu.Items.Add(MenuItem("ProjectPlugins", () => this.EditProjectPluginsAsync(item.Project)));
        menu.Items.Add(new MenuFlyoutSeparator());
        menu.Items.Add(MenuItem("Delete", () => this.ViewModel.DeleteProjectAsync(item)));
        menu.ShowAt(element, e.GetPosition(element));
    }

    private async Task<string?> PromptAsync(string title, string initial)
    {
        var input = new TextBox { Text = initial, PlaceholderText = Strings.Get("Name") };
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = title,
            Content = input,
            PrimaryButtonText = Strings.Get("Ok"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = this.ActualTheme,
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(input.Text) ? input.Text.Trim() : null;
    }

    private async Task EditProjectPluginsAsync(Project project)
    {
        var plugins = this.ViewModel.Services.Catalog.DiscoverPlugins(this.ViewModel.Services.Settings.RootDirectory);
        var panel = new StackPanel { Spacing = 4 };
        panel.Children.Add(new TextBlock { Text = Strings.Get("ProjectPluginsHint"), TextWrapping = TextWrapping.Wrap, Opacity = 0.7 });
        var boxes = plugins
            .Select(p => new CheckBox { Content = p.Name, IsChecked = project.PluginScope?.Contains(p.Name, StringComparer.OrdinalIgnoreCase) == true })
            .ToList();
        foreach (var box in boxes)
        {
            panel.Children.Add(box);
        }

        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = project.Name,
            Content = new ScrollViewer { Content = panel, MaxHeight = 360 },
            PrimaryButtonText = Strings.Get("Save"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Primary,
            RequestedTheme = this.ActualTheme,
        };
        if (await dialog.ShowAsync() != ContentDialogResult.Primary)
        {
            return;
        }

        var selected = boxes.Where(b => b.IsChecked == true).Select(b => (string)b.Content).ToList();
        await this.ViewModel.UpdateProjectAsync(project with { PluginScope = selected.Count == 0 ? null : selected });
    }
}
