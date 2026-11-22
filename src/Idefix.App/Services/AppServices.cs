using System.Diagnostics.CodeAnalysis;
using Idefix.Core.Chat;
using Idefix.Core.Indexing;
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

    public static readonly string DataDirectory = Environment.GetEnvironmentVariable("IDEFIX_DATA_DIR") is { Length: > 0 } overridden
        ? overridden
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Idefix");

    private readonly ISettingsStore settingsStore;
    private readonly DispatcherQueue dispatcher;
    private readonly SemaphoreSlim indexingGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private bool reindexPending;

    private AppServices(ISettingsStore settingsStore, ISessionStore sessionStore, IdefixSettings settings, DispatcherQueue dispatcher)
    {
        this.settingsStore = settingsStore;
        this.Sessions = sessionStore;
        this.Settings = settings;
        this.dispatcher = dispatcher;
        this.HttpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        this.Models = new OpenAiCompatibleModelCatalog(this.HttpClient);
        this.Updates = new VelopackUpdateService(Repository);
        this.IndexStatus = Strings.Get("IndexIdle");
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
    public partial string IndexStatus { get; set; }

    [ObservableProperty]
    public partial bool IsIndexing { get; set; }

    [ObservableProperty]
    public partial bool IsUpdateReady { get; set; }

    public bool IsConfigured => this.Runtime is not null;

    public static async Task<AppServices> CreateAsync(DispatcherQueue dispatcher)
    {
        var appData = DataDirectory;
        var settingsStore = new DpapiSettingsStore(Path.Combine(appData, "profile.dat"));
        var settings = await LoadSettingsAsync(settingsStore);
        Strings.Initialize(settings.Language);
        var services = new AppServices(settingsStore, new JsonSessionStore(appData), settings, dispatcher);
        services.RebuildRuntime();
        return services;
    }

    public async Task UpdateSettingsAsync(IdefixSettings settings)
    {
        var previous = this.Settings;
        this.Settings = settings;
        await this.settingsStore.SaveAsync(settings, this.lifetime.Token);
        if (previous.RootDirectory != settings.RootDirectory || previous.Llm != settings.Llm
            || previous.PrePrompt != settings.PrePrompt || previous.ContextMode != settings.ContextMode)
        {
            this.RebuildRuntime();
        }

        this.SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RequestReindex() => _ = this.ReindexAsync();

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Update checks are best-effort and must never crash the app.")]
    public async Task<string> CheckForUpdatesAsync()
    {
        try
        {
            var status = await this.Updates.PrepareAsync(this.Settings.NightlyUpdates, this.lifetime.Token);
            this.IsUpdateReady = status == UpdateStatus.ReadyToApply;
            return status switch
            {
                UpdateStatus.ReadyToApply => Strings.Get("UpdateReady"),
                UpdateStatus.UpToDate => Strings.Get("UpToDate"),
                _ => Strings.Get("UpdatesNotInstalled"),
            };
        }
        catch (Exception ex)
        {
            return Strings.Format("UpdateFailedFormat", ex.Message);
        }
    }

    public void Dispose()
    {
        this.lifetime.Cancel();
        this.lifetime.Dispose();
        this.Runtime?.Dispose();
        this.HttpClient.Dispose();
        this.indexingGate.Dispose();
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
    private void RebuildRuntime()
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
            this.RequestReindex();
        }
        catch (Exception ex)
        {
            this.IndexStatus = Strings.Format("IndexFailedFormat", ex.Message);
        }
    }

    private void OnKnowledgeChanged(object? sender, EventArgs e) => this.RequestReindex();

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Background indexing reports errors in the UI instead of crashing.")]
    private async Task ReindexAsync()
    {
        if (this.Runtime is not { } runtime)
        {
            return;
        }

        if (!await this.indexingGate.WaitAsync(TimeSpan.Zero, this.lifetime.Token))
        {
            this.reindexPending = true;
            return;
        }

        try
        {
            do
            {
                this.reindexPending = false;
                this.OnUi(() => this.IsIndexing = true);
                var progress = new Progress<IndexingProgress>(p => this.OnUi(() =>
                    this.IndexStatus = p.IsComplete ? Strings.Get("IndexIdle") : Strings.Format("IndexingFormat", p.Completed + 1, p.Total)));
                var result = await Task.Run(() => runtime.Indexer.SynchronizeAsync(runtime.Settings.RootDirectory, progress, this.lifetime.Token), this.lifetime.Token);
                if (result.Failures.Count > 0)
                {
                    this.OnUi(() => this.IndexStatus = Strings.Format("IndexFailuresFormat", result.Failures.Count, result.Failures[0].Reason.ReplaceLineEndings(" ")));
                }
            }
            while (this.reindexPending && ReferenceEquals(runtime, this.Runtime));
        }
        catch (OperationCanceledException) when (this.lifetime.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            this.OnUi(() => this.IndexStatus = Strings.Format("IndexFailedFormat", ex.Message));
        }
        finally
        {
            this.OnUi(() => this.IsIndexing = false);
            this.indexingGate.Release();
        }
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
