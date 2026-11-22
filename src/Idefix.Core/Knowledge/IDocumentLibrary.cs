namespace Idefix.Core.Knowledge;

public interface IDocumentLibrary
{
    Task<LibrarySnapshot> LoadAsync(IReadOnlyCollection<string>? plugins, IProgress<LibraryProgress>? progress, CancellationToken cancellationToken);
}
