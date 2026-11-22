using Idefix.App.ViewModels;

namespace Idefix.App.Views;

public sealed partial class SplashView : UserControl
{
    public SplashView(SplashViewModel viewModel)
    {
        this.ViewModel = viewModel;
        this.InitializeComponent();
    }

    public SplashViewModel ViewModel { get; }
}
