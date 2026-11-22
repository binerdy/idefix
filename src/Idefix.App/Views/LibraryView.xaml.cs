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
}
