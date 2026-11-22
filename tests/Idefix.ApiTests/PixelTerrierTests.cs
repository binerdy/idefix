using Idefix.Core.Mascot;

namespace Idefix.ApiTests;

public sealed class PixelTerrierTests
{
    [Fact]
    public void EveryFrameFillsTheSameCanvas()
    {
        for (var t = 0.0; t < PixelTerrier.Cycle; t += 0.05)
        {
            var frame = PixelTerrier.Compose(t);
            Assert.Equal(PixelTerrier.CanvasHeight, frame.Length);
            Assert.All(frame, row => Assert.Equal(PixelTerrier.CanvasWidth, row.Length));
        }
    }

    [Fact]
    public void SittingFrameShowsCollarAndNoPage()
    {
        var frame = PixelTerrier.Compose(0.2);

        Assert.True(Count(frame, 'R') > 0);
        Assert.Equal(0, Count(frame, 'Q'));
        Assert.Equal(0, Count(frame, 'D'));
    }

    [Fact]
    public void SniffingLowersTheHead()
    {
        Assert.True(FirstRow(PixelTerrier.Compose(1.8), 'N') > FirstRow(PixelTerrier.Compose(0.2), 'N'));
    }

    [Fact]
    public void DiggingSpraysDirtAndLiftsPaws()
    {
        var left = PixelTerrier.Compose(2.70);
        var right = PixelTerrier.Compose(2.88);

        Assert.True(Count(left, 'D') > 0);
        Assert.NotEqual(Render(left), Render(right));
    }

    [Fact]
    public void TheDogTrotsInPlaceCarryingThePage()
    {
        var first = PixelTerrier.Compose(5.04);
        var second = PixelTerrier.Compose(5.22);

        Assert.True(Count(first, 'Q') > 0);
        Assert.True(Count(second, 'Q') > 0);
        Assert.NotEqual(FirstRow(first, 'N'), FirstRow(second, 'N'));
        Assert.Equal(Bounds(first, 'W').Left, Bounds(second, 'W').Left);
    }

    [Fact]
    public void TheCycleLoops()
    {
        Assert.Equal(Render(PixelTerrier.Compose(0.2)), Render(PixelTerrier.Compose(PixelTerrier.Cycle + 0.2)));
    }

    private static int Count(char[][] frame, char pixel) => frame.Sum(row => row.Count(c => c == pixel));

    private static int FirstRow(char[][] frame, char pixel) => Array.FindIndex(frame, row => row.Contains(pixel));

    private static (int Left, int Right) Bounds(char[][] frame, char pixel)
    {
        var columns = frame.SelectMany(row => row.Select((c, i) => (c, i)).Where(p => p.c == pixel).Select(p => p.i)).ToList();
        return (columns.Min(), columns.Max());
    }

    private static string Render(char[][] frame) => string.Join('\n', frame.Select(row => new string(row)));
}
