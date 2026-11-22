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
        foreach (var step in message.Trace)
        {
            this.Trace.Add(new TraceStepViewModel(step));
        }
    }

    private MessageViewModel()
    {
        this.Text = string.Empty;
        this.Citations = [];
        this.IsStreaming = true;
    }

    public bool IsUser { get; }

    public bool IsAssistant => !this.IsUser;

    public ObservableCollection<TraceStepViewModel> Trace { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTrace), nameof(TraceSummary))]
    public partial bool IsStreaming { get; set; }

    [ObservableProperty]
    public partial string Text { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<Citation> Citations { get; set; }

    public bool HasTrace => this.Trace.Count > 0 || this.IsStreaming;

    public string TraceSummary => this.IsStreaming && this.Trace.Count == 0
        ? Strings.Get("Searching")
        : Strings.Format("TraceTitleFormat", this.Trace.Count);

    public static MessageViewModel StartAssistant() => new();

    public void AppendText(string delta)
    {
        this.streamed.Append(delta);
        this.Text = this.streamed.ToString();
    }

    public void AddTrace(TraceStep step)
    {
        this.Trace.Add(new TraceStepViewModel(step));
        this.OnPropertyChanged(nameof(this.HasTrace));
        this.OnPropertyChanged(nameof(this.TraceSummary));
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
}
