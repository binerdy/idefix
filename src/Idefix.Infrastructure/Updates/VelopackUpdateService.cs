using Idefix.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Idefix.Infrastructure.Updates;

public sealed class VelopackUpdateService(Uri repository) : IUpdateService
{
    private UpdateManager? manager;
    private UpdateInfo? pending;

    public async Task<UpdateCheck> PrepareAsync(bool includeNightly, CancellationToken cancellationToken)
    {
        var source = new GithubSource(repository.ToString(), accessToken: null, prerelease: includeNightly);
        var current = new UpdateManager(source);
        if (!current.IsInstalled)
        {
            return new UpdateCheck(UpdateStatus.NotInstalled);
        }

        var update = await current.CheckForUpdatesAsync();
        if (update is null)
        {
            return this.pending is null ? new UpdateCheck(UpdateStatus.UpToDate) : Ready(this.pending);
        }

        if (this.pending?.TargetFullRelease.Version == update.TargetFullRelease.Version)
        {
            return Ready(this.pending);
        }

        await current.DownloadUpdatesAsync(update, progress: null, cancelToken: cancellationToken);
        this.manager = current;
        this.pending = update;

        // Update.exe waits in the background and installs the download as soon as Idefix exits.
        current.WaitExitThenApplyUpdates(update.TargetFullRelease, silent: true, restart: false);
        return Ready(update);
    }

    public void ApplyAndRestart()
    {
        if (this.manager is not null && this.pending is not null)
        {
            this.manager.ApplyUpdatesAndRestart(this.pending.TargetFullRelease);
        }
    }

    private static UpdateCheck Ready(UpdateInfo update) =>
        new(UpdateStatus.ReadyToApply, update.TargetFullRelease.Version.ToString());
}
