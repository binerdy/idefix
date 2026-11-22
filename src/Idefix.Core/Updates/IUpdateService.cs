namespace Idefix.Core.Updates;

public interface IUpdateService
{
    Task<UpdateStatus> PrepareAsync(bool includeNightly, CancellationToken cancellationToken);

    void ApplyAndRestart();
}
