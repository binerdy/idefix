using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Idefix.App.Services;
using Idefix.Core.Settings;

namespace Idefix.App.ViewModels;

public sealed partial class ProfileViewModel : ObservableObject
{
    private readonly AppServices services;

    public ProfileViewModel(AppServices services)
    {
        this.services = services;
        this.FirstName = string.Empty;
        this.RootDirectory = string.Empty;
        this.Endpoint = string.Empty;
        this.ApiKey = string.Empty;
        this.ChatModel = string.Empty;
        this.PrePrompt = string.Empty;
        this.Status = string.Empty;
        this.UpdateStatus = string.Empty;
        this.Load();
    }

    public event EventHandler? LanguageChanged;

    public ReadOnlyCollection<string> Providers { get; } = new([Strings.Get("ProviderOllama"), Strings.Get("ProviderOpenAI"), Strings.Get("ProviderCompatible")]);

    public ReadOnlyCollection<string> Languages { get; } = new([Strings.Get("LanguageSystem"), Strings.Get("LanguageEnglish"), Strings.Get("LanguageGerman")]);

    public ObservableCollection<string> AvailableModels { get; } = [];

    public string Version { get; } = Strings.Format(
        "VersionFormat",
        typeof(ProfileViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev");

    [ObservableProperty]
    public partial string FirstName { get; set; }

    [ObservableProperty]
    public partial string RootDirectory { get; set; }

    [ObservableProperty]
    public partial int ProviderIndex { get; set; }

    [ObservableProperty]
    public partial string Endpoint { get; set; }

    [ObservableProperty]
    public partial string ApiKey { get; set; }

    [ObservableProperty]
    public partial string ChatModel { get; set; }

    [ObservableProperty]
    public partial double MaxContextTokens { get; set; }

    [ObservableProperty]
    public partial string PrePrompt { get; set; }

    [ObservableProperty]
    public partial int LanguageIndex { get; set; }

    [ObservableProperty]
    public partial bool NightlyUpdates { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; }

    [ObservableProperty]
    public partial string UpdateStatus { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    public bool NeedsSetup => !this.services.IsConfigured;

    public void Load()
    {
        var settings = this.services.Settings;
        this.FirstName = settings.FirstName;
        this.RootDirectory = settings.RootDirectory;
        this.ProviderIndex = (int)settings.Llm.Provider;
        this.Endpoint = settings.Llm.Endpoint?.ToString() ?? string.Empty;
        this.ApiKey = settings.Llm.ApiKey;
        this.ChatModel = settings.Llm.ChatModel;
        this.MaxContextTokens = settings.Llm.MaxContextTokens;
        this.PrePrompt = settings.PrePrompt;
        this.LanguageIndex = (int)settings.Language;
        this.NightlyUpdates = settings.NightlyUpdates;
        this.UpdateStatus = this.services.UpdateStatusText;
        this.Status = string.Empty;
        this.OnPropertyChanged(nameof(this.NeedsSetup));
    }

    public void ApplyPickedRoot(string path) => this.RootDirectory = path;

    [RelayCommand]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Any provider error is shown next to the button.")]
    public async Task DiscoverModelsAsync()
    {
        this.IsBusy = true;
        try
        {
            var models = await this.services.Models.ListModelsAsync(this.BuildLlm(), CancellationToken.None);
            this.AvailableModels.Clear();
            foreach (var model in models)
            {
                this.AvailableModels.Add(model);
            }

            this.Status = Strings.Format("ModelsFoundFormat", models.Count);
        }
        catch (Exception ex)
        {
            this.Status = Strings.Format("ModelsFailedFormat", ex.Message);
        }
        finally
        {
            this.IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SaveAsync()
    {
        var previousLanguage = this.services.Settings.Language;
        var settings = this.services.Settings with
        {
            FirstName = this.FirstName.Trim(),
            RootDirectory = this.RootDirectory.Trim(),
            Llm = this.BuildLlm(),
            PrePrompt = this.PrePrompt,
            Language = (AppLanguage)this.LanguageIndex,
            NightlyUpdates = this.NightlyUpdates,
        };

        await this.services.UpdateSettingsAsync(settings);
        this.Status = Strings.Get("Saved");
        this.OnPropertyChanged(nameof(this.NeedsSetup));
        if (previousLanguage != settings.Language)
        {
            this.LanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    public async Task CheckUpdatesAsync()
    {
        this.UpdateStatus = string.Empty;
        this.UpdateStatus = await this.services.CheckForUpdatesAsync();
    }

    private static int ContextTokens(double value) =>
        double.IsNaN(value) || value < 1_000 ? LlmSettings.DefaultContextTokens : (int)Math.Min(value, int.MaxValue);

    private LlmSettings BuildLlm()
    {
        var endpoint = Uri.TryCreate(this.Endpoint.Trim(), UriKind.Absolute, out var parsed) ? LlmSettings.AsBaseAddress(parsed) : null;
        return new LlmSettings((LlmProviderKind)this.ProviderIndex, endpoint, this.ApiKey.Trim(), this.ChatModel.Trim(), ContextTokens(this.MaxContextTokens));
    }
}
