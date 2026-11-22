using Idefix.App.Services;
using Idefix.App.ViewModels;
using Idefix.App.Views;
using Idefix.Core.Settings;

namespace Idefix.App;

public sealed partial class IdefixApplication : Application
{
    private Window? window;
    private AppServices? services;

    public IdefixApplication()
    {
        this.InitializeComponent();
        this.UnhandledException += (_, e) => CrashLog.Write(e.Exception);
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        this.window = new Window { Title = "Idefix", ExtendsContentIntoTitleBar = true };
        this.window.Closed += (_, _) => this.services?.Dispose();

        this.services = await AppServices.CreateAsync(this.window.DispatcherQueue);
        this.services.WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this.window);
        this.services.SettingsChanged += (_, _) => this.ApplyTheme();

        await this.ShowShellAsync();
        this.window.Activate();
        _ = this.services.CheckForUpdatesAsync();
    }

    private async Task ShowShellAsync()
    {
        var app = this.services!;
        Strings.Initialize(app.Settings.Language);
        var viewModel = new ShellViewModel(app);
        viewModel.Profile.LanguageChanged += async (_, _) => await this.ShowShellAsync();
        var shell = new ShellPage(viewModel);
        shell.Profile.WindowHandle = app.WindowHandle;
        this.window!.Content = shell;
        this.window.SetTitleBar(shell.TitleBarDragRegion);
        shell.ActualThemeChanged += (sender, _) => this.ApplyCaptionColors(sender.ActualTheme);
        this.ApplyTheme();
        await viewModel.LoadAsync();
    }

    private void ApplyTheme()
    {
        if (this.window?.Content is FrameworkElement root && this.services is { } app)
        {
            root.RequestedTheme = app.Settings.Theme switch
            {
                AppTheme.Light => ElementTheme.Light,
                AppTheme.Dark => ElementTheme.Dark,
                _ => ElementTheme.Default,
            };
            this.ApplyCaptionColors(root.ActualTheme);
        }
    }

    private void ApplyCaptionColors(ElementTheme theme)
    {
        if (this.window is null)
        {
            return;
        }

        var titleBar = this.window.AppWindow.TitleBar;
        var foreground = theme == ElementTheme.Dark ? Microsoft.UI.Colors.White : Microsoft.UI.Colors.Black;
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = foreground;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonHoverBackgroundColor = theme == ElementTheme.Dark
            ? Windows.UI.Color.FromArgb(40, 255, 255, 255)
            : Windows.UI.Color.FromArgb(20, 0, 0, 0);
    }
}
