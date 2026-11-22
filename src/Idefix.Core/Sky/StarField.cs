using System.Diagnostics.CodeAnalysis;

namespace Idefix.Core.Sky;

public static class StarField
{
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Decorative star placement.")]
    [SuppressMessage("Security", "S2245:Using pseudorandom number generators (PRNGs) is security-sensitive", Justification = "Decorative star placement.")]
    public static IReadOnlyList<Star> Generate(int seed, int count)
    {
        var random = new Random(seed);
        return Enumerable.Range(0, count)
            .Select(_ => new Star(
                random.NextDouble(),
                random.NextDouble(),
                1.5 + (random.NextDouble() * 3.5),
                random.NextDouble() * Math.PI * 2,
                6 + (random.NextDouble() * 10)))
            .ToList();
    }
}
