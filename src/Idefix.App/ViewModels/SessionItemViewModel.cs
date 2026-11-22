using Idefix.App.Services;
using Idefix.Core.Chat;

namespace Idefix.App.ViewModels;

public sealed partial class SessionItemViewModel(ChatSession session) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial ChatSession Session { get; set; } = session;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public Guid Id => this.Session.Id;

    public string Title => string.IsNullOrWhiteSpace(this.Session.Title) ? Strings.Get("Untitled") : this.Session.Title;
}
