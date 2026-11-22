using System.Globalization;
using System.Text.Json;
using Idefix.Core.Chat;

namespace Idefix.Infrastructure.Persistence;

public sealed class JsonSessionStore(string directory) : ISessionStore
{
    private readonly string sessionsDirectory = Path.Combine(directory, "sessions");
    private readonly string projectsFile = Path.Combine(directory, "projects.json");

    public async Task<IReadOnlyList<Project>> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(this.projectsFile))
        {
            return [];
        }

        await using var stream = File.OpenRead(this.projectsFile);
        return await JsonSerializer.DeserializeAsync(stream, IdefixJsonContext.Default.ListProject, cancellationToken) ?? [];
    }

    public async Task SaveProjectAsync(Project project, CancellationToken cancellationToken)
    {
        var projects = (await this.LoadProjectsAsync(cancellationToken)).Where(p => p.Id != project.Id).Append(project).ToList();
        await this.WriteProjectsAsync(projects, cancellationToken);
    }

    public async Task DeleteProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var projects = (await this.LoadProjectsAsync(cancellationToken)).Where(p => p.Id != projectId).ToList();
        await this.WriteProjectsAsync(projects, cancellationToken);
        foreach (var session in (await this.LoadSessionsAsync(cancellationToken)).Where(s => s.ProjectId == projectId))
        {
            await this.SaveSessionAsync(session with { ProjectId = null }, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<ChatSession>> LoadSessionsAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(this.sessionsDirectory))
        {
            return [];
        }

        var sessions = new List<ChatSession>();
        foreach (var file in Directory.EnumerateFiles(this.sessionsDirectory, "*.json"))
        {
            await using var stream = File.OpenRead(file);
            if (await JsonSerializer.DeserializeAsync(stream, IdefixJsonContext.Default.ChatSession, cancellationToken) is { } session)
            {
                sessions.Add(session);
            }
        }

        return sessions.OrderByDescending(s => s.CreatedAt).ToList();
    }

    public async Task SaveSessionAsync(ChatSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(this.sessionsDirectory);
        await WriteAtomicallyAsync(
            this.SessionPath(session.Id),
            stream => JsonSerializer.SerializeAsync(stream, session, IdefixJsonContext.Default.ChatSession, cancellationToken),
            cancellationToken);
    }

    public Task DeleteSessionAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        File.Delete(this.SessionPath(sessionId));
        return Task.CompletedTask;
    }

    private static async Task WriteAtomicallyAsync(string path, Func<Stream, Task> write, CancellationToken cancellationToken)
    {
        var temporary = path + ".tmp";
        var stream = File.Create(temporary);
        await using (stream.ConfigureAwait(false))
        {
            await write(stream);
            await stream.FlushAsync(cancellationToken);
        }

        File.Move(temporary, path, overwrite: true);
    }

    private Task WriteProjectsAsync(List<Project> projects, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(this.projectsFile)!);
        return WriteAtomicallyAsync(
            this.projectsFile,
            stream => JsonSerializer.SerializeAsync(stream, projects, IdefixJsonContext.Default.ListProject, cancellationToken),
            cancellationToken);
    }

    private string SessionPath(Guid id) => Path.Combine(this.sessionsDirectory, id.ToString("N", CultureInfo.InvariantCulture) + ".json");
}
