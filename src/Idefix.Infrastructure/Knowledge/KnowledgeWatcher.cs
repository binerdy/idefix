using Idefix.Core.Knowledge;

namespace Idefix.Infrastructure.Knowledge;

public sealed class KnowledgeWatcher : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly ITimer debounce;
    private readonly TimeSpan quietPeriod;

    public KnowledgeWatcher(string rootDirectory, TimeSpan quietPeriod, TimeProvider timeProvider)
    {
        this.quietPeriod = quietPeriod;
        this.debounce = timeProvider.CreateTimer(_ => this.Changed?.Invoke(this, EventArgs.Empty), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        this.watcher = new FileSystemWatcher(rootDirectory)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        this.watcher.Created += this.OnFileSystemEvent;
        this.watcher.Changed += this.OnFileSystemEvent;
        this.watcher.Deleted += this.OnFileSystemEvent;
        this.watcher.Renamed += this.OnFileSystemEvent;
        this.watcher.EnableRaisingEvents = true;
    }

    public event EventHandler? Changed;

    public void Dispose()
    {
        this.watcher.Dispose();
        this.debounce.Dispose();
    }

    private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.Contains(KnowledgeLayout.IndexFolderName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        this.debounce.Change(this.quietPeriod, Timeout.InfiniteTimeSpan);
    }
}
