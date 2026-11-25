using System.Diagnostics.CodeAnalysis;
using Idefix.Core.Sky;
using Microsoft.UI.Dispatching;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.UI.ViewManagement;

namespace Idefix.App.Controls;

public sealed partial class CelestialLayer : Grid
{
    private const int StarCount = 46;
    private static readonly TimeSpan FrameInterval = TimeSpan.FromMilliseconds(66);
    private static readonly TimeSpan IdleInterval = TimeSpan.FromSeconds(30);

    private readonly SKXamlCanvas canvas = new();
    private readonly IReadOnlyList<Star> stars = StarField.Generate(seed: 1707, StarCount);
    private readonly Random random = new();
    private readonly UISettings uiSettings = new();
    private readonly DateTimeOffset startedAt = TimeProvider.System.GetUtcNow();
    private DispatcherQueueTimer? timer;
    private SkyPoint? lastPointer;
    private ShootingStar? shootingStar;
    private DateTimeOffset shootingStarStartedAt;
    private DateTimeOffset nextShootingStarAt;

    public CelestialLayer()
    {
        this.IsHitTestVisible = false;
        this.canvas.IsHitTestVisible = false;
        this.canvas.PaintSurface += this.OnPaintSurface;
        this.Children.Add(this.canvas);
        this.Loaded += this.OnLoaded;
        this.Unloaded += this.OnUnloaded;
        this.ActualThemeChanged += (_, _) => this.Restart();
    }

    private bool IsNightSky => this.ActualTheme == ElementTheme.Dark;

    public void TrackPointer(Windows.Foundation.Point position) => this.lastPointer = new SkyPoint(position.X, position.Y);

    private static SKPath Sparkle(float x, float y, float radius)
    {
        var waist = radius * 0.28f;
        using var builder = new SKPathBuilder();
        builder.MoveTo(x, y - radius);
        builder.LineTo(x + waist, y - waist);
        builder.LineTo(x + radius, y);
        builder.LineTo(x + waist, y + waist);
        builder.LineTo(x, y + radius);
        builder.LineTo(x - waist, y + waist);
        builder.LineTo(x - radius, y);
        builder.LineTo(x - waist, y - waist);
        builder.Close();
        return builder.Detach();
    }

    private static void DrawMoon(SKCanvas surface, SkyPoint center)
    {
        var x = (float)center.X;
        var y = (float)center.Y;
        const float radius = 26;
        using var glow = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(new SKPoint(x, y), radius * 3, [new SKColor(230, 232, 240, 28), SKColors.Transparent], SKShaderTileMode.Clamp),
        };
        surface.DrawCircle(x, y, radius * 3, glow);

        using var body = new SKPaint { IsAntialias = true, Color = new SKColor(226, 228, 236, 46) };
        surface.DrawCircle(x, y, radius, body);

        using var outline = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1, Color = new SKColor(226, 228, 236, 70) };
        surface.DrawCircle(x, y, radius, outline);
        surface.DrawCircle(x - 10, y - 7, 4, outline);
        surface.DrawCircle(x + 7, y - 6, 3.5f, outline);
        surface.DrawCircle(x - 6, y + 8, 3, outline);
        surface.DrawCircle(x + 9, y + 9, 2, outline);
    }

    private static void DrawSun(SKCanvas surface, SkyPoint center)
    {
        var point = new SKPoint((float)center.X, (float)center.Y);
        using var halo = new SKPaint
        {
            IsAntialias = true,
            Shader = SKShader.CreateRadialGradient(point, 160, [new SKColor(255, 196, 92, 46), new SKColor(255, 214, 140, 14), SKColors.Transparent], [0, 0.45f, 1], SKShaderTileMode.Clamp),
        };
        surface.DrawCircle(point, 160, halo);
        using var disc = new SKPaint { IsAntialias = true, Color = new SKColor(255, 200, 110, 34) };
        surface.DrawCircle(point, 26, disc);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        this.timer = this.DispatcherQueue.CreateTimer();
        this.timer.Tick += (_, _) => this.canvas.Invalidate();
        this.Restart();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        this.timer?.Stop();
        this.timer = null;
    }

    private void Restart()
    {
        if (this.timer is null)
        {
            return;
        }

        this.ScheduleShootingStar(TimeProvider.System.GetUtcNow());
        this.timer.Interval = this.IsNightSky && this.uiSettings.AnimationsEnabled ? FrameInterval : IdleInterval;
        this.timer.Start();
        this.canvas.Invalidate();
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Decorative timing.")]
    private void ScheduleShootingStar(DateTimeOffset now) => this.nextShootingStarAt = now + ShootingStarPlanner.NextDelay(this.random);

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var surface = e.Surface.Canvas;
        surface.Clear(SKColors.Transparent);
        if (this.ActualWidth <= 0 || this.ActualHeight <= 0)
        {
            return;
        }

        var scale = (float)(e.Info.Width / this.ActualWidth);
        surface.Scale(scale);
        var width = this.ActualWidth;
        var height = this.ActualHeight;
        var now = TimeProvider.System.GetUtcNow();

        if (this.IsNightSky)
        {
            this.DrawStars(surface, width, height, (now - this.startedAt).TotalSeconds);
            if (SkyClock.Local.MoonProgress(now) is { } moon)
            {
                DrawMoon(surface, SkyClock.ArcPoint(moon, width, height));
            }

            this.DrawShootingStar(surface, width, height, now);
        }
        else if (SkyClock.Local.SunProgress(now) is { } sun)
        {
            DrawSun(surface, SkyClock.ArcPoint(sun, width, height));
        }
    }

    private void DrawStars(SKCanvas surface, double width, double height, double seconds)
    {
        var animate = this.uiSettings.AnimationsEnabled;
        using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
        foreach (var star in this.stars)
        {
            var alpha = (byte)(255 * (animate ? star.Brightness(seconds) : 0.22));
            paint.Color = new SKColor(214, 222, 240, alpha);
            using var path = Sparkle((float)(star.X * width), (float)(star.Y * height), (float)star.Radius);
            surface.DrawPath(path, paint);
        }
    }

    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Decorative placement.")]
    private void DrawShootingStar(SKCanvas surface, double width, double height, DateTimeOffset now)
    {
        if (!this.uiSettings.AnimationsEnabled)
        {
            return;
        }

        if (this.shootingStar is null && now >= this.nextShootingStarAt)
        {
            this.shootingStar = ShootingStarPlanner.Plan(this.random, width, height, this.lastPointer);
            this.shootingStarStartedAt = now;
        }

        if (this.shootingStar is not { } star)
        {
            return;
        }

        var progress = (now - this.shootingStarStartedAt) / star.Duration;
        if (progress >= 1)
        {
            this.shootingStar = null;
            this.ScheduleShootingStar(now);
            return;
        }

        var head = star.Head(progress);
        var tail = star.Head(Math.Max(0, progress - 0.35));
        var alpha = (byte)(255 * ShootingStar.Opacity(progress));
        using var paint = new SKPaint
        {
            IsAntialias = true,
            StrokeWidth = 1.4f,
            StrokeCap = SKStrokeCap.Round,
            Shader = SKShader.CreateLinearGradient(
                new SKPoint((float)tail.X, (float)tail.Y),
                new SKPoint((float)head.X, (float)head.Y),
                [SKColors.Transparent, new SKColor(236, 240, 255, alpha)],
                SKShaderTileMode.Clamp),
        };
        surface.DrawLine((float)tail.X, (float)tail.Y, (float)head.X, (float)head.Y, paint);
    }
}
