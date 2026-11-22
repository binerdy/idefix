using Idefix.App.Services;
using Idefix.App.ViewModels;

namespace Idefix.App.Views;

public sealed partial class LibraryView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(LibraryViewModel), typeof(LibraryView), new PropertyMetadata(null));

    public LibraryView()
    {
        this.InitializeComponent();
    }

    public LibraryViewModel ViewModel
    {
        get => (LibraryViewModel)this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    private async void OnRebuildAllClicked(object sender, RoutedEventArgs e) => await this.RebuildAsync(plugin: null);

    private async void OnRebuildPluginClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string plugin })
        {
            await this.RebuildAsync(plugin);
        }
    }

    private async Task RebuildAsync(string? plugin)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = this.XamlRoot,
            Title = Strings.Get("RebuildMarkdown"),
            Content = new TextBlock
            {
                Text = plugin is null ? Strings.Get("RebuildMarkdownAllHint") : Strings.Format("RebuildMarkdownPluginHintFormat", plugin),
                TextWrapping = TextWrapping.Wrap,
            },
            PrimaryButtonText = Strings.Get("RebuildMarkdown"),
            CloseButtonText = Strings.Get("Cancel"),
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = this.ActualTheme,
        };
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await this.ViewModel.RebuildMarkdownAsync(plugin);
        }
    }
}
