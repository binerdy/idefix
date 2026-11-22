using System.Collections.ObjectModel;
using Idefix.App.Services;
using Idefix.Core.Chat;

namespace Idefix.App.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly List<ChatSession> sessions = [];
    private readonly List<Project> projects = [];

    public ShellViewModel(AppServices services)
    {
        this.Services = services;
        this.Library = new LibraryViewModel(services);
        this.Profile = new ProfileViewModel(services);
        this.Viewer = new DocumentViewerViewModel(services);
        this.Chat = this.CreateChat(project: null, isTemporary: false);
        this.SearchText = string.Empty;
        this.IsSidebarOpen = true;
        services.SettingsChanged += (_, _) => this.OnSettingsChanged();
    }

    public AppServices Services { get; }

    public LibraryViewModel Library { get; }

    public ProfileViewModel Profile { get; }

    public DocumentViewerViewModel Viewer { get; }

    public ObservableCollection<SessionItemViewModel> LooseSessions { get; } = [];

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = [];

    public IReadOnlyList<Project> AllProjects => this.projects;

    public string FirstName => this.Services.Settings.FirstName;

    public string Initials => string.IsNullOrWhiteSpace(this.FirstName) ? "?" : this.FirstName[..1].ToUpperInvariant();

    [ObservableProperty]
    public partial ChatViewModel Chat { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChat), nameof(IsLibrary), nameof(IsProfile))]
    public partial ShellSection Section { get; set; }

    [ObservableProperty]
    public partial bool IsSidebarOpen { get; set; }

    [ObservableProperty]
    public partial bool IsSearchOpen { get; set; }

    [ObservableProperty]
    public partial string SearchText { get; set; }

    [ObservableProperty]
    public partial bool IsTemporaryChat { get; set; }

    public bool IsChat => this.Section == ShellSection.Chat;

    public bool IsLibrary => this.Section == ShellSection.Library;

    public bool IsProfile => this.Section == ShellSection.Profile;

    public async Task LoadAsync()
    {
        this.projects.AddRange(await this.Services.Sessions.LoadProjectsAsync(CancellationToken.None));
        this.sessions.AddRange(await this.Services.Sessions.LoadSessionsAsync(CancellationToken.None));
        this.RebuildTree();
        if (!this.Services.IsConfigured)
        {
            this.Section = ShellSection.Profile;
        }
    }

    [RelayCommand]
    public void NewChat() => this.StartChat(project: null);

    [RelayCommand]
    public void NewChatInProject(ProjectItemViewModel project) => this.StartChat(project.Project);

    [RelayCommand]
    public void ToggleTemporaryChat()
    {
        this.IsTemporaryChat = !this.IsTemporaryChat;
        this.Chat = this.CreateChat(project: null, this.IsTemporaryChat);
        this.Section = ShellSection.Chat;
        this.MarkSelected(null);
    }

    [RelayCommand]
    public void OpenSession(SessionItemViewModel item)
    {
        var project = this.projects.Find(p => p.Id == item.Session.ProjectId);
        this.IsTemporaryChat = false;
        this.Chat = new ChatViewModel(this.Services, item.Session, project, isTemporary: false, this.OnSessionSaved);
        this.Section = ShellSection.Chat;
        this.MarkSelected(item.Id);
    }

    [RelayCommand]
    public void OpenLibrary()
    {
        this.Library.Refresh();
        this.Section = ShellSection.Library;
    }

    [RelayCommand]
    public void OpenProfile()
    {
        this.Profile.Load();
        this.Section = ShellSection.Profile;
    }

    [RelayCommand]
    public void ToggleSidebar() => this.IsSidebarOpen = !this.IsSidebarOpen;

    [RelayCommand]
    public void ToggleSearch()
    {
        this.IsSearchOpen = !this.IsSearchOpen;
        if (!this.IsSearchOpen)
        {
            this.SearchText = string.Empty;
        }
    }

    public async Task CreateProjectAsync(string name)
    {
        var project = new Project(Guid.NewGuid(), name.Trim());
        await this.Services.Sessions.SaveProjectAsync(project, CancellationToken.None);
        this.projects.Add(project);
        this.RebuildTree();
    }

    public async Task UpdateProjectAsync(Project project)
    {
        await this.Services.Sessions.SaveProjectAsync(project, CancellationToken.None);
        this.projects[this.projects.FindIndex(p => p.Id == project.Id)] = project;
        this.RebuildTree();
    }

    public async Task DeleteProjectAsync(ProjectItemViewModel item)
    {
        await this.Services.Sessions.DeleteProjectAsync(item.Project.Id, CancellationToken.None);
        this.projects.RemoveAll(p => p.Id == item.Project.Id);
        for (var i = 0; i < this.sessions.Count; i++)
        {
            if (this.sessions[i].ProjectId == item.Project.Id)
            {
                this.sessions[i] = this.sessions[i] with { ProjectId = null };
            }
        }

        this.RebuildTree();
    }

    public async Task UpdateSessionAsync(ChatSession session)
    {
        await this.Services.Sessions.SaveSessionAsync(session, CancellationToken.None);
        this.OnSessionSaved(session);
    }

    public async Task DeleteSessionAsync(SessionItemViewModel item)
    {
        await this.Services.Sessions.DeleteSessionAsync(item.Id, CancellationToken.None);
        this.sessions.RemoveAll(s => s.Id == item.Id);
        if (this.Chat.SessionId == item.Id)
        {
            this.NewChat();
        }

        this.RebuildTree();
    }

    partial void OnSearchTextChanged(string value) => this.RebuildTree();

    private void StartChat(Project? project)
    {
        this.IsTemporaryChat = false;
        this.Chat = this.CreateChat(project, isTemporary: false);
        this.Section = ShellSection.Chat;
        this.MarkSelected(null);
    }

    private ChatViewModel CreateChat(Project? project, bool isTemporary) =>
        new(this.Services, ChatSession.Start(project?.Id, this.Services.Time), project, isTemporary, this.OnSessionSaved);

    private void OnSessionSaved(ChatSession session)
    {
        var index = this.sessions.FindIndex(s => s.Id == session.Id);
        if (index >= 0)
        {
            this.sessions[index] = session;
        }
        else
        {
            this.sessions.Insert(0, session);
        }

        this.RebuildTree();
        this.MarkSelected(this.Chat.SessionId);
    }

    private void OnSettingsChanged()
    {
        this.OnPropertyChanged(nameof(this.FirstName));
        this.OnPropertyChanged(nameof(this.Initials));
        this.Chat.RefreshPlugins();
        this.Library.Refresh();
    }

    private void RebuildTree()
    {
        var visible = this.sessions
            .Where(s => string.IsNullOrWhiteSpace(this.SearchText) || s.Title.Contains(this.SearchText, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        this.LooseSessions.Clear();
        foreach (var session in visible.Where(s => s.ProjectId is null || this.projects.TrueForAll(p => p.Id != s.ProjectId)))
        {
            this.LooseSessions.Add(new SessionItemViewModel(session));
        }

        var expanded = this.Projects.Where(p => !p.IsExpanded).Select(p => p.Project.Id).ToHashSet();
        this.Projects.Clear();
        foreach (var project in this.projects.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new ProjectItemViewModel(project) { IsExpanded = !expanded.Contains(project.Id) };
            foreach (var session in visible.Where(s => s.ProjectId == project.Id))
            {
                item.Sessions.Add(new SessionItemViewModel(session));
            }

            this.Projects.Add(item);
        }

        this.MarkSelected(this.Chat.SessionId);
    }

    private void MarkSelected(Guid? sessionId)
    {
        foreach (var item in this.LooseSessions.Concat(this.Projects.SelectMany(p => p.Sessions)))
        {
            item.IsSelected = item.Id == sessionId;
        }
    }
}
