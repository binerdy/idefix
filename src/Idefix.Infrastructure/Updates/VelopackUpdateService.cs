using Idefix.Core.Updates;
using Velopack;
using Velopack.Sources;

namespace Idefix.Infrastructure.Updates;

public sealed class VelopackUpdateService(Uri repository) : IUpdateService
{
    private UpdateManager? manager;
    private UpdateInfo? pending;

    public async Task<UpdateStatus> PrepareAsync(bool includeNightly, CancellationToken cancellationToken)
    {
        var source = new GithubSource(repository.ToString(), accessToken: null, prerelease: includeNightly);
        this.manager = new UpdateManager(source);
        if (!this.manager.IsInstalled)
        {
            return UpdateStatus.NotInstalled;
        }

        var update = await this.manager.CheckForUpdatesAsync();
        if (update is null)
        {
            return UpdateStatus.UpToDate;
        }

        await this.manager.DownloadUpdatesAsync(update, progress: null, cancelToken: cancellationToken);
        this.pending = update;
        return UpdateStatus.ReadyToApply;
    }

    public void ApplyAndRestart()
    {
        if (this.manager is not null && this.pending is not null)
        {
            this.manager.ApplyUpdatesAndRestart(this.pending.TargetFullRelease);
        }
    }
}
