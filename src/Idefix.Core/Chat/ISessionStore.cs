namespace Idefix.Core.Chat;

public interface ISessionStore
{
    Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken);

    Task SaveProjectAsync(Project project, CancellationToken cancellationToken);

    Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ChatSession>> LoadSessionsAsync(CancellationToken cancellationToken);

    Task SaveSessionAsync(ChatSession session, CancellationToken cancellationToken);

    Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken);
}
