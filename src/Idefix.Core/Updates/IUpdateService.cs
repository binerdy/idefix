namespace Idefix.Core.Updates;

public interface IUpdateService
{
    Task<UpdateCheck> PrepareAsync(bool includeNightly, CancellationToken cancellationToken);

    void ApplyAndRestart();
}
