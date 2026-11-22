namespace Idefix.Core.Indexing;

public sealed record IndexingFailure(string DocumentKey, string Reason);
