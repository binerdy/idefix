namespace Idefix.Core.Knowledge;

public static class PluginScope
{
    public static IReadOnlyList<string> Resolve(
        IEnumerable<string> available,
        IReadOnlyCollection<string> globallyDisabled,
        IReadOnlyCollection<string>? projectScope,
        IReadOnlyCollection<string> sessionDisabled)
    {
        return available
            .Where(p => !globallyDisabled.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Where(p => projectScope is null || projectScope.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Where(p => !sessionDisabled.Contains(p, StringComparer.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
