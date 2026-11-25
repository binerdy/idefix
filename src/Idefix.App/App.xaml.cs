using System.Diagnostics;
using Idefix.App.Services;
using Idefix.App.ViewModels;
using Idefix.App.Views;
using Idefix.Core.Knowledge;
using Idefix.Core.Settings;
using Idefix.Core.Sky;
using Microsoft.UI.Dispatching;

namespace Idefix.App;

public sealed partial class IdefixApplication : Application
{
    private static readonly TimeSpan MinimumSplashTime = TimeSpan.FromSeconds(
        double.TryParse(Environment.GetEnvironmentVariable("IDEFIX_SPLASH_SECONDS"), System.Globalization.CultureInfo.InvariantCulture, out var seconds) ? seconds : 1.6);

    private static readonly TimeSpan ThemeCheckInterval = TimeSpan.FromMinutes(1);

    private Window? window;
    private DispatcherQueueTimer? themeTimer;
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

        Strings.Initialize(AppLanguage.System);
        var splash = new SplashViewModel();
        this.window.Content = new SplashView(splash);
        this.ApplyCaptionColors(ElementTheme.Dark);
        this.window.Activate();
        var shown = Stopwatch.StartNew();

        splash.Report(0.1, "SplashLoadingProfile");
        this.services = await AppServices.CreateAsync(this.window.DispatcherQueue);
        this.services.WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this.window);

        await ReportKnowledgeAsync(this.services, splash);

        splash.Report(0.8, "SplashLoadingChats");
        var shell = await this.CreateShellAsync();

        splash.Report(0.9, "SplashCheckingUpdates");
        try
        {
            await this.services.StartUpdateChecksAsync().WaitAsync(TimeSpan.FromSeconds(2.5));
        }
        catch (TimeoutException)
        {
            splash.Report(0.95, "SplashCheckingUpdates");
        }

        splash.Report(1, "SplashReady");
        var remaining = MinimumSplashTime - shown.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining);
        }

        this.Show(shell);
    }

    private static async Task ReportKnowledgeAsync(AppServices app, SplashViewModel splash)
    {
        splash.Report(0.3, "SplashSearchingFolders");
        var root = app.Settings.RootDirectory;
        var (plugins, documents) = await Task.Run(() =>
        {
            var found = app.Catalog.DiscoverPlugins(root);
            return (found.Count, found.Sum(p => app.Catalog.ScanDocuments(p).Count));
        });
        splash.Report(0.5, "SplashFoundFormat", documents, plugins);

        if (app.IsConfigured)
        {
            var reading = new Progress<LibraryProgress>(p =>
                splash.Report(0.5 + (0.25 * p.Completed / Math.Max(1, p.Total)), "SplashReadingFormat", Math.Min(p.Completed + 1, p.Total), p.Total));
            await app.ReadLibraryAsync(reading);
        }
    }

    private async Task<ShellPage> CreateShellAsync()
    {
        var app = this.services!;
        Strings.Initialize(app.Settings.Language);
        var viewModel = new ShellViewModel(app);
        viewModel.Profile.LanguageChanged += async (_, _) => this.Show(await this.CreateShellAsync());
        var shell = new ShellPage(viewModel);
        shell.Profile.WindowHandle = app.WindowHandle;
        await viewModel.LoadAsync();
        return shell;
    }

    private void Show(ShellPage shell)
    {
        this.window!.Content = shell;
        this.window.SetTitleBar(shell.TitleBarDragRegion);
        shell.ActualThemeChanged += (sender, _) => this.ApplyCaptionColors(sender.ActualTheme);
        this.ApplyTheme();
        if (this.themeTimer is null)
        {
            this.themeTimer = this.window.DispatcherQueue.CreateTimer();
            this.themeTimer.Interval = ThemeCheckInterval;
            this.themeTimer.Tick += (_, _) => this.ApplyTheme();
            this.themeTimer.Start();
        }
    }

    private void ApplyTheme()
    {
        if (this.window?.Content is not FrameworkElement root)
        {
            return;
        }

        var theme = SkyClock.Local.IsDay(TimeProvider.System.GetLocalNow()) ? ElementTheme.Light : ElementTheme.Dark;
        if (root.RequestedTheme != theme)
        {
            root.RequestedTheme = theme;
            this.ApplyCaptionColors(theme);
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
