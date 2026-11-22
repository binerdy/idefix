using Idefix.App.ViewModels;
using Windows.Storage.Pickers;

namespace Idefix.App.Views;

public sealed partial class ProfileView : UserControl
{
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ProfileViewModel), typeof(ProfileView), new PropertyMetadata(null));

    public ProfileView()
    {
        this.InitializeComponent();
    }

    public ProfileViewModel ViewModel
    {
        get => (ProfileViewModel)this.GetValue(ViewModelProperty);
        set => this.SetValue(ViewModelProperty, value);
    }

    public nint WindowHandle { get; set; }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        var showSettings = ReferenceEquals(sender.SelectedItem, this.SettingsTab);
        this.ProfilePanel.Visibility = showSettings ? Visibility.Collapsed : Visibility.Visible;
        this.SettingsPanel.Visibility = showSettings ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void OnBrowseClicked(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, this.WindowHandle);
        if (await picker.PickSingleFolderAsync() is { } folder)
        {
            this.ViewModel.ApplyPickedRoot(folder.Path);
        }
    }
}
