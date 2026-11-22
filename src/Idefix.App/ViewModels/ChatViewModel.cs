using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using Idefix.App.Services;
using Idefix.Core.Agent;
using Idefix.Core.Chat;
using Idefix.Core.Knowledge;

namespace Idefix.App.ViewModels;

public sealed partial class ChatViewModel : ObservableObject, IDisposable
{
    private readonly AppServices services;
    private readonly Project? project;
    private readonly Action<ChatSession> saved;
    private ChatSession session;
    private CancellationTokenSource? answering;

    public ChatViewModel(AppServices services, ChatSession session, Project? project, bool isTemporary, Action<ChatSession> saved)
    {
        this.services = services;
        this.session = session;
        this.project = project;
        this.IsTemporary = isTemporary;
        this.saved = saved;
        this.Prompt = string.Empty;
        this.DeepThinking = session.DeepThinking;
        foreach (var message in session.Messages)
        {
            this.Messages.Add(new MessageViewModel(message));
        }

        this.Messages.CollectionChanged += (_, _) => this.OnPropertyChanged(nameof(this.IsEmpty));
        this.RefreshPlugins();
    }

    public Guid SessionId => this.session.Id;

    public bool IsTemporary { get; }

    public ObservableCollection<MessageViewModel> Messages { get; } = [];

    public ObservableCollection<PluginToggleViewModel> SessionPlugins { get; } = [];

    public bool IsEmpty => this.Messages.Count == 0;

    public bool HasNoPlugins => this.SessionPlugins.Count == 0;

    public bool NeedsSetup => !this.services.IsConfigured;

    public string Greeting => Strings.Format("GreetingFormat", this.services.Settings.FirstName);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial string Prompt { get; set; }

    [ObservableProperty]
    public partial bool DeepThinking { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsBusy { get; set; }

    public void RefreshPlugins()
    {
        var available = this.services.Catalog.DiscoverPlugins(this.services.Settings.RootDirectory).Select(p => p.Name);
        var scoped = PluginScope.Resolve(available, this.services.Settings.DisabledPlugins, this.project?.PluginScope, []);
        this.SessionPlugins.Clear();
        foreach (var name in scoped)
        {
            var enabled = !this.session.DisabledPlugins.Contains(name, StringComparer.OrdinalIgnoreCase);
            this.SessionPlugins.Add(new PluginToggleViewModel(name, enabled, this.OnPluginToggled));
        }

        this.OnPropertyChanged(nameof(this.HasNoPlugins));
        this.OnPropertyChanged(nameof(this.NeedsSetup));
        this.OnPropertyChanged(nameof(this.Greeting));
    }

    [RelayCommand]
    public void Stop() => this.answering?.Cancel();

    public void Dispose() => this.answering?.Dispose();

    partial void OnDeepThinkingChanged(bool value) => this.session = this.session with { DeepThinking = value };

    private bool CanSend() => !this.IsBusy && !string.IsNullOrWhiteSpace(this.Prompt);

    [RelayCommand(CanExecute = nameof(CanSend))]
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Provider failures are shown in the conversation.")]
    private async Task SendAsync()
    {
        if (this.services.Runtime is not { } runtime)
        {
            this.OnPropertyChanged(nameof(this.NeedsSetup));
            return;
        }

        var question = this.Prompt.Trim();
        this.Prompt = string.Empty;
        this.IsBusy = true;
        this.answering = new CancellationTokenSource();

        var userMessage = ChatMessage.FromUser(question, this.services.Time);
        var history = this.session.Messages;
        this.session = this.session.Append(userMessage);
        this.Messages.Add(new MessageViewModel(userMessage));
        var reply = MessageViewModel.StartAssistant();
        this.Messages.Add(reply);

        var plugins = PluginScope.Resolve(
            runtime.Plugins().Select(p => p.Name),
            this.services.Settings.DisabledPlugins,
            this.project?.PluginScope,
            this.session.DisabledPlugins);

        try
        {
            var request = new AnswerRequest(question, history, plugins, this.DeepThinking);
            await foreach (var answerEvent in runtime.AnswerEngine.AskAsync(request, this.answering.Token))
            {
                switch (answerEvent)
                {
                    case SelectingSections selecting:
                        reply.ShowSelecting(selecting.Round, selecting.TotalSections);
                        break;
                    case SectionsPicked picked:
                        reply.AddRound(picked.Round);
                        break;
                    case ContextPrepared prepared:
                        reply.ShowContext(prepared.Summary);
                        break;
                    case TextDelta delta:
                        reply.AppendText(delta.Text);
                        break;
                    case AnswerCompleted completed:
                        reply.Complete(completed.Message);
                        this.session = this.session.Append(completed.Message);
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            reply.IsStreaming = false;
        }
        catch (ContextTooLargeException tooLarge)
        {
            reply.Fail(Strings.Format(
                "ContextTooLargeFormat",
                AppServices.FormatTokens(tooLarge.EstimatedTokens),
                AppServices.FormatTokens(tooLarge.Limit)));
        }
        catch (Exception ex)
        {
            reply.Fail(Strings.Format("AnswerFailedFormat", ex.Message));
        }
        finally
        {
            this.answering.Dispose();
            this.answering = null;
            this.IsBusy = false;
            await this.SaveAsync();
        }
    }

    private void OnPluginToggled(PluginToggleViewModel toggle)
    {
        var disabled = this.session.DisabledPlugins.Where(p => !string.Equals(p, toggle.Name, StringComparison.OrdinalIgnoreCase));
        this.session = this.session with { DisabledPlugins = (toggle.IsEnabled ? disabled : disabled.Append(toggle.Name)).ToList() };
        _ = this.SaveAsync();
    }

    private async Task SaveAsync()
    {
        if (this.IsTemporary || this.session.Messages.Count == 0)
        {
            return;
        }

        await this.services.Sessions.SaveSessionAsync(this.session, CancellationToken.None);
        this.saved(this.session);
    }
}
