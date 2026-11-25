using Idefix.Core.Knowledge;

namespace Idefix.Infrastructure.Knowledge;

public sealed class KnowledgeWatcher : IDisposable
{
    private readonly FileSystemWatcher watcher;
    private readonly ITimer debounce;
    private readonly TimeSpan quietPeriod;
    private readonly string rootDirectory;

    public KnowledgeWatcher(string rootDirectory, TimeSpan quietPeriod, TimeProvider timeProvider)
    {
        this.quietPeriod = quietPeriod;
        this.rootDirectory = rootDirectory;
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
        // Idefix writes the _markdown folders itself; reacting to them would re-read the library in a loop.
        if (KnowledgeLayout.IsInMarkdownFolder(Path.GetRelativePath(this.rootDirectory, e.FullPath))
            || (!KnowledgeLayout.IsSupportedDocument(e.FullPath) && Path.HasExtension(e.FullPath)))
        {
            return;
        }

        this.debounce.Change(this.quietPeriod, Timeout.InfiniteTimeSpan);
    }
}
