using System.Collections.ObjectModel;
using System.Text;
using Idefix.App.Services;
using Idefix.Core.Chat;
using Idefix.Core.Documents;

namespace Idefix.App.ViewModels;

public sealed partial class MessageViewModel : ObservableObject
{
    private readonly StringBuilder streamed = new();

    public MessageViewModel(ChatMessage message)
    {
        this.IsUser = message.Role == ChatRole.User;
        this.Text = message.Content;
        this.Citations = message.Citations;
        this.ShowContext(message.Context);
    }

    private MessageViewModel()
    {
        this.Text = string.Empty;
        this.Citations = [];
        this.IsStreaming = true;
    }

    public bool IsUser { get; }

    public bool IsAssistant => !this.IsUser;

    public ObservableCollection<string> Documents { get; } = [];

    public ObservableCollection<string> Rounds { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext), nameof(ContextHeadline))]
    public partial bool IsStreaming { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasContext), nameof(ContextHeadline))]
    public partial int EstimatedTokens { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextHeadline))]
    public partial int TotalSections { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextHeadline))]
    public partial bool IsSelecting { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ContextHeadline))]
    public partial int Round { get; set; }

    public bool HasRounds => this.Rounds.Count > 0;

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Citation> Citations { get; set; }

    public bool HasContext => this.IsStreaming || this.Documents.Count > 0;

    public string ContextHeadline
    {
        get
        {
            if (this.Documents.Count == 0)
            {
                return this.IsSelecting ? Strings.Format("SelectingRoundFormat", this.Round, this.TotalSections) : Strings.Get("ReadingDocuments");
            }

            var tokens = AppServices.FormatTokens(this.EstimatedTokens);
            return this.TotalSections > 0
                ? Strings.Format("SectionsContextFormat", this.Documents.Count, this.TotalSections, tokens)
                : Strings.Format("ContextFormat", this.Documents.Count, tokens);
        }
    }

    public static MessageViewModel StartAssistant() => new();

    public void AppendText(string delta)
    {
        this.streamed.Append(delta);
        this.Text = this.streamed.ToString();
    }

    public void ShowContext(ContextSummary? summary)
    {
        this.Documents.Clear();
        if (summary is null)
        {
            return;
        }

        if (this.Rounds.Count == 0)
        {
            foreach (var round in summary.Rounds ?? [])
            {
                this.Rounds.Add(FormatRound(round));
            }
        }

        foreach (var document in summary.Documents)
        {
            this.Documents.Add(document);
        }

        this.EstimatedTokens = summary.EstimatedTokens;
        this.TotalSections = summary.TotalSections;
        this.IsSelecting = false;
        this.OnPropertyChanged(nameof(this.HasContext));
        this.OnPropertyChanged(nameof(this.HasRounds));
        this.OnPropertyChanged(nameof(this.ContextHeadline));
    }

    public void ShowSelecting(int round, int totalSections)
    {
        this.Round = round;
        this.TotalSections = totalSections;
        this.IsSelecting = true;
    }

    public void AddRound(SelectionRound round)
    {
        this.Rounds.Add(FormatRound(round));
        this.OnPropertyChanged(nameof(this.HasRounds));
    }

    public void Complete(ChatMessage message)
    {
        this.Text = message.Content;
        this.Citations = message.Citations;
        this.IsStreaming = false;
    }

    public void Fail(string error)
    {
        this.Text = error;
        this.IsStreaming = false;
    }

    private static string FormatRound(SelectionRound round) =>
        Strings.Format("RoundFormat", round.Number, string.Join(" · ", round.Added));
}
