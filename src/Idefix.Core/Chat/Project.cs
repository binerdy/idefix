namespace Idefix.Core.Chat;

public sealed record Project(Guid Id, string Name, IReadOnlyList<string>? PluginScope = null);
