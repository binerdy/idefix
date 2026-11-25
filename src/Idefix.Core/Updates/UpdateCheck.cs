namespace Idefix.Core.Updates;

public sealed record UpdateCheck(UpdateStatus Status, string? Version = null);
