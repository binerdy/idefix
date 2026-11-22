namespace Idefix.Core.Indexing;

public sealed record IndexingResult(int Indexed, int Removed, IReadOnlyList<IndexingFailure> Failures);
