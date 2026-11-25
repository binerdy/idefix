using Idefix.Core.Mascot;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI.ViewManagement;

namespace Idefix.App.Controls;

public sealed partial class PixelMatrix : Grid
{
    private const int Border = 2;
    private const double RevealSeconds = 0.9;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(40);

    private static readonly Dictionary<char, SKColor> Palette = new()
    {
        ['K'] = new SKColor(0x5A, 0x60, 0x6E),
        ['W'] = new SKColor(0xF7, 0xF7, 0xF4),
        ['S'] = new SKColor(0xC9, 0xCE, 0xD8),
        ['G'] = new SKColor(0x8A, 0x8F, 0x99),
        ['E'] = new SKColor(0x05, 0x06, 0x0A),
        ['N'] = new SKColor(0x05, 0x06, 0x0A),
        ['O'] = new SKColor(0xFF, 0xFF, 0xFF),
        ['R'] = new SKColor(0xD9, 0x53, 0x4F),
        ['Y'] = new SKColor(0xF2, 0xC1, 0x4E),
        ['P'] = new SKColor(0xF0, 0x8A, 0x9A),
        ['Q'] = new SKColor(0xDC, 0xE6, 0xF5),
        ['L'] = new SKColor(0x8C, 0x9A, 0xB2),
        ['D'] = new SKColor(0x8B, 0x6B, 0x4A),
        ['A'] = new SKColor(0x9F, 0xA6, 0xB2),
    };

    private readonly SKXamlCanvas canvas = new();
    private readonly bool animate = new UISettings().AnimationsEnabled
        || Environment.GetEnvironmentVariable("IDEFIX_FORCE_ANIMATIONS") == "1";

    private readonly DateTimeOffset startedAt = TimeProvider.System.GetUtcNow();
    private DispatcherQueueTimer? timer;

    public PixelMatrix()
    {
        this.IsHitTestVisible = false;
        this.canvas.PaintSurface += this.OnPaintSurface;
        this.Children.Add(this.canvas);
        this.Loaded += this.OnLoaded;
        this.Unloaded += (_, _) => this.timer?.Stop();
    }

    private static int Columns => PixelTerrier.CanvasWidth + (2 * Border);

    private static int Rows => PixelTerrier.CanvasHeight + (2 * Border);

    private static double Reveal(double elapsed, int row) =>
        Math.Clamp((elapsed - (RevealSeconds * row / PixelTerrier.CanvasHeight)) / 0.25, 0, 1);

    private static bool IsEye(char pixel) => pixel is 'E' or 'O';

    private static SKColor Blend(SKColor from, SKColor to, float amount) => new(
        (byte)(from.Red + ((to.Red - from.Red) * amount)),
        (byte)(from.Green + ((to.Green - from.Green) * amount)),
        (byte)(from.Blue + ((to.Blue - from.Blue) * amount)));

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!this.animate)
        {
            this.canvas.Invalidate();
            return;
        }

        this.timer = this.DispatcherQueue.CreateTimer();
        this.timer.Interval = FrameInterval;
        this.timer.Tick += (_, _) => this.canvas.Invalidate();
        this.timer.Start();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface.Canvas;
        surface.Clear(SKColors.Transparent);
        var elapsed = this.animate ? (TimeProvider.System.GetUtcNow() - this.startedAt).TotalSeconds : 10;
        var cell = Math.Min(e.Info.Width / (float)Columns, e.Info.Height / (float)Rows);
        var originX = (e.Info.Width - (cell * Columns)) / 2;
        var originY = (e.Info.Height - (cell * Rows)) / 2;
        var frame = PixelTerrier.Compose(this.animate ? Math.Max(0, elapsed - RevealSeconds) : 0);
        var blinking = this.animate && elapsed % 3.6 is > 3.45;
        var scanRow = this.animate && elapsed > RevealSeconds ? (int)((elapsed - RevealSeconds) * 12) % (Rows + 8) : -100;

        using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
        var gap = Math.Max(1f, cell * 0.06f);
        for (var row = 0; row < Rows; row++)
        {
            for (var column = 0; column < Columns; column++)
            {
                var spriteRow = row - Border;
                var spriteColumn = column - Border;
                var pixel = spriteRow >= 0 && spriteRow < PixelTerrier.CanvasHeight && spriteColumn >= 0 && spriteColumn < PixelTerrier.CanvasWidth
                    ? frame[spriteRow][spriteColumn]
                    : '.';

                if (!Palette.TryGetValue(pixel, out var color))
                {
                    continue;
                }

                if (blinking && IsEye(pixel))
                {
                    color = Palette['G'];
                }

                var glow = Math.Max(0, 1 - (Math.Abs(row - scanRow) / 2.5));
                if (glow > 0)
                {
                    color = Blend(color, new SKColor(0x9F, 0xC8, 0xFF), (float)(glow * 0.25));
                }

                paint.Color = color.WithAlpha((byte)(255 * Reveal(elapsed, spriteRow)));
                var x = originX + (column * cell);
                var y = originY + (row * cell);
                surface.DrawRect(x, y, cell - gap, cell - gap, paint);
            }
        }
    }
}
