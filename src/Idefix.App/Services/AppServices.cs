using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Idefix.Core.Chat;
using Idefix.Core.Knowledge;
using Idefix.Core.Settings;
using Idefix.Core.Updates;
using Idefix.Infrastructure;
using Idefix.Infrastructure.Knowledge;
using Idefix.Infrastructure.Llm;
using Idefix.Infrastructure.Persistence;
using Idefix.Infrastructure.Updates;
using Microsoft.UI.Dispatching;

namespace Idefix.App.Services;

public sealed partial class AppServices : ObservableObject, IDisposable
{
    public static readonly Uri Repository = new("https://github.com/binerdy/idefix");

    // Velopack installs the program into %LOCALAPPDATA%\Idefix and deletes that folder on uninstall, so user data lives next to it.
    public static readonly string LegacyDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Idefix");

    public static readonly string DataDirectory = Environment.GetEnvironmentVariable("IDEFIX_DATA_DIR") is { Length: > 0 } overridden
        ? overridden
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Idefix.Data");

    private static readonly TimeSpan UpdateInterval = TimeSpan.FromHours(4);

    private readonly ISettingsStore settingsStore;
    private readonly DispatcherQueue dispatcher;
    private readonly SemaphoreSlim readingGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private bool readPending;

    private AppServices(ISettingsStore settingsStore, ISessionStore sessionStore, IdefixSettings settings, DispatcherQueue dispatcher)
    {
        this.settingsStore = settingsStore;
        this.Sessions = sessionStore;
        this.Settings = settings;
        this.dispatcher = dispatcher;
        this.HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        this.Models = new OpenAiCompatibleModelCatalog(this.HttpClient);
        this.Updates = new VelopackUpdateService(Repository);
        this.LibraryStatus = string.Empty;
    }

    public event EventHandler? SettingsChanged;

    public IdefixSettings Settings { get; private set; }

    public IdefixRuntime? Runtime { get; private set; }

    public ISessionStore Sessions { get; }

    public IModelCatalog Models { get; }

    public IUpdateService Updates { get; }

    public HttpClient HttpClient { get; }

    public FileSystemKnowledgeCatalog Catalog { get; } = new();

    public TimeProvider Time { get; } = TimeProvider.System;

    public nint WindowHandle { get; set; }

    [ObservableProperty]
    public partial string LibraryStatus { get; set; }

    [ObservableProperty]
    public partial bool IsReading { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateReady { get; set; }

    [ObservableProperty]
    public partial string UpdateMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string UpdateStatusText { get; set; } = string.Empty;

    public bool IsConfigured => this.Runtime is not null;

    public static string FormatTokens(int tokens) => tokens >= 1_000
        ? string.Create(CultureInfo.CurrentCulture, $"{tokens / 1000.0:0.#}k")
        : tokens.ToString(CultureInfo.CurrentCulture);

    public static async Task<AppServices> CreateAsync(DispatcherQueue dispatcher)
    {
        var appData = DataDirectory;
        if (Environment.GetEnvironmentVariable("IDEFIX_DATA_DIR") is not { Length: > 0 })
        {
            MigrateLegacyData(appData);
        }

        var settingsStore = new DpapiSettingsStore(Path.Combine(appData, "profile.dat"));
        var settings = await LoadSettingsAsync(settingsStore);
        Strings.Initialize(settings.Language);
        var services = new AppServices(settingsStore, new JsonSessionStore(appData), settings, dispatcher);
        services.RebuildRuntime(readLibrary: false);
        return services;
    }

    public async Task UpdateSettingsAsync(IdefixSettings settings)
    {
        var previous = this.Settings;
        this.Settings = settings;
        await this.settingsStore.SaveAsync(settings, this.lifetime.Token);
        if (previous.RootDirectory != settings.RootDirectory || previous.Llm != settings.Llm || previous.PrePrompt != settings.PrePrompt)
        {
            this.RebuildRuntime(readLibrary: true);
        }

        this.SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestLibraryRead() => _ = this.ReadLibraryAsync(observer: null);

    public async Task RebuildMarkdownAsync(string? plugin)
    {
        if (this.Runtime is not { } runtime)
        {
            return;
        }

        this.IsReading = true;
        try
        {
            var snapshot = await Task.Run(() => runtime.Library.LoadAsync(plugin is null ? null : (string[])[plugin], progress: null, this.lifetime.Token), this.lifetime.Token);
            await Task.Run(() => runtime.Sections.RebuildAsync(snapshot.Documents, this.lifetime.Token), this.lifetime.Token);
        }
        finally
        {
            this.IsReading = false;
        }

        this.RequestLibraryRead();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background reading reports errors in the UI instead of crashing.")]
    public async Task ReadLibraryAsync(IProgress<LibraryProgress>? observer)
    {
        if (this.Runtime is not { } runtime)
        {
            return;
        }

        if (!await this.readingGate.WaitAsync(TimeSpan.Zero, this.lifetime.Token))
        {
            this.readPending = true;
            return;
        }

        try
        {
            do
            {
                this.readPending = false;
                this.OnUi(() => this.IsReading = true);
                var progress = new Progress<LibraryProgress>(p =>
                {
                    observer?.Report(p);
                    this.OnUi(() => this.LibraryStatus = Strings.Format("LibraryReadingFormat", Math.Min(p.Completed + 1, p.Total), p.Total));
                });
                var snapshot = await Task.Run(() => runtime.Library.LoadAsync(plugins: null, progress, this.lifetime.Token), this.lifetime.Token);
                var sections = await Task.Run(() => runtime.Sections.SynchronizeAsync(snapshot.Documents, this.lifetime.Token), this.lifetime.Token);
                await runtime.Sections.PruneAsync(snapshot.Documents.Select(d => d.Document).ToList(), this.lifetime.Token);
                var tokens = sections.Sum(s => s.EstimatedTokens);
                var status = snapshot.Failures.Count > 0
                    ? Strings.Format("LibraryFailuresFormat", snapshot.Failures.Count, snapshot.Failures[0].Reason.ReplaceLineEndings(" "))
                    : Strings.Format("LibraryReadyFormat", snapshot.Documents.Count, sections.Count, FormatTokens(tokens));
                this.OnUi(() => this.LibraryStatus = status);
            }
            while (this.readPending && ReferenceEquals(runtime, this.Runtime));
        }
        catch (OperationCanceledException) when (this.lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            this.OnUi(() => this.LibraryStatus = Strings.Format("LibraryFailedFormat", ex.Message));
        }
        finally
        {
            this.OnUi(() => this.IsReading = false);
            this.readingGate.Release();
        }
    }

    public Task StartUpdateChecksAsync()
    {
        var first = this.CheckForUpdatesAsync();
        _ = this.CheckPeriodicallyAsync(first);
        return first;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Update checks are best-effort and must never crash the app.")]
    public async Task<string> CheckForUpdatesAsync()
    {
        string message;
        try
        {
            var check = await this.Updates.PrepareAsync(this.Settings.NightlyUpdates, this.lifetime.Token);
            message = check.Status switch
            {
                UpdateStatus.ReadyToApply => Strings.Format("UpdateReadyFormat", check.Version ?? string.Empty),
                UpdateStatus.UpToDate => Strings.Get("UpToDate"),
                _ => Strings.Get("UpdatesNotInstalled"),
            };
            this.OnUi(() =>
            {
                this.IsUpdateReady = check.Status == UpdateStatus.ReadyToApply;
                this.UpdateMessage = this.IsUpdateReady ? message : string.Empty;
            });
        }
        catch (OperationCanceledException) when (this.lifetime.IsCancellationRequested)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            message = Strings.Format("UpdateFailedFormat", ex.Message);
            CrashLog.Append("update.log", ex.ToString());
        }

        var stamped = Strings.Format("UpdateCheckedFormat", DateTimeOffset.Now.ToString("t", CultureInfo.CurrentCulture), message);
        this.OnUi(() => this.UpdateStatusText = stamped);
        return stamped;
    }

    public void Dispose()
    {
        this.lifetime.Cancel();
        this.lifetime.Dispose();
        this.Runtime?.Dispose();
        this.HttpClient.Dispose();
        this.readingGate.Dispose();
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed move leaves the data where it was; the app still starts.")]
    private static void MigrateLegacyData(string dataDirectory)
    {
        try
        {
            var moved = DataDirectoryMigration.Migrate(LegacyDataDirectory, dataDirectory);
            if (moved.Count > 0)
            {
                CrashLog.Append("migration.log", $"Moved {string.Join(", ", moved)} from {LegacyDataDirectory} to {dataDirectory}.");
            }
        }
        catch (Exception ex)
        {
            CrashLog.Append("migration.log", ex.ToString());
        }
    }

    private static async Task<IdefixSettings> LoadSettingsAsync(DpapiSettingsStore store)
    {
        try
        {
            if (await store.LoadAsync(CancellationToken.None) is { } stored)
            {
                return stored;
            }
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            // A profile encrypted by another Windows user cannot be read; start fresh instead of failing.
        }

        return IdefixSettings.CreateDefault(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), await WindowsFirstNameAsync());
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Account details are optional; the account name is a fine fallback.")]
    private static async Task<string> WindowsFirstNameAsync()
    {
        try
        {
            var users = await Windows.System.User.FindAllAsync();
            if (users.Count > 0 && await users[0].GetPropertyAsync(Windows.System.KnownUserProperties.FirstName) is string { Length: > 0 } firstName)
            {
                return firstName;
            }
        }
        catch (Exception)
        {
            return Environment.UserName;
        }

        return Environment.UserName;
    }

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "An invalid provider configuration leaves the app usable in setup mode.")]
    private void RebuildRuntime(bool readLibrary)
    {
        if (this.Runtime is { } old)
        {
            old.KnowledgeChanged -= this.OnKnowledgeChanged;
            old.Dispose();
            this.Runtime = null;
        }

        if (SettingsValidator.Validate(this.Settings).Count > 0)
        {
            return;
        }

        try
        {
            this.Runtime = IdefixRuntime.Create(this.Settings, this.HttpClient, this.Time);
            this.Runtime.KnowledgeChanged += this.OnKnowledgeChanged;
            if (readLibrary)
            {
                this.RequestLibraryRead();
            }
        }
        catch (Exception ex)
        {
            this.LibraryStatus = Strings.Format("LibraryFailedFormat", ex.Message);
        }
    }

    private void OnKnowledgeChanged(object? sender, EventArgs e) => this.RequestLibraryRead();

    private async Task CheckPeriodicallyAsync(Task first)
    {
        await first;
        while (!this.lifetime.IsCancellationRequested)
        {
            await Task.Delay(UpdateInterval, this.lifetime.Token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            if (!this.lifetime.IsCancellationRequested)
            {
                await this.OnUiAsync(this.CheckForUpdatesAsync);
            }
        }
    }

    private Task OnUiAsync(Func<Task> action)
    {
        var done = new TaskCompletionSource();
        this.dispatcher.TryEnqueue(async () =>
        {
            await action();
            done.SetResult();
        });
        return done.Task;
    }

    private void OnUi(Action action)
    {
        if (this.dispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            this.dispatcher.TryEnqueue(() => action());
        }
    }
}
