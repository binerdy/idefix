namespace Idefix.Core.Knowledge;

public sealed record LibrarySnapshot(IReadOnlyList<LoadedDocument> Documents, IReadOnlyList<DocumentFailure> Failures);
