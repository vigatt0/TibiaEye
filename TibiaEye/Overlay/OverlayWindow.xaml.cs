using System.Runtime.Versioning;
using System.Windows;
using SkiaSharp;
using SkiaSharp.Views.Desktop;
using TibiaEye.Core;
using TibiaEye.Models;

namespace TibiaEye.Overlay;

/// <summary>
/// Transparent, always-on-top HUD overlay drawn with SkiaSharp.
/// Shows:
///  • HP / Mana percentage bars.
///  • Cooldown progress circles for active spells (Haste, Mana-Shield).
/// </summary>
[SupportedOSPlatform("windows")]
public partial class OverlayWindow : Window
{
    // ── State ─────────────────────────────────────────────────────────────────
    private double _hpPct;
    private double _manaPct;
    private IReadOnlyDictionary<string, (double Remaining, double Total)> _cooldowns
        = new Dictionary<string, (double, double)>();

    private readonly System.Windows.Threading.DispatcherTimer _repaintTimer;

    // ── Colours ───────────────────────────────────────────────────────────────
    private static readonly SKColor HpBarColor   = new(219,  79,  79, 220);
    private static readonly SKColor ManaBarColor = new( 83,  80, 218, 220);
    private static readonly SKColor BackBarColor = new( 40,  40,  40, 180);
    private static readonly SKColor TextColor    = SKColors.White;
    private static readonly SKColor CooldownArc  = new(255, 200,  50, 230);
    private static readonly SKColor CooldownBg   = new( 60,  60,  60, 180);

    // ── Constructor ───────────────────────────────────────────────────────────
    public OverlayWindow()
    {
        InitializeComponent();

        // Redraw at ~30 fps
        _repaintTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _repaintTimer.Tick += (_, _) => SkiaCanvas.InvalidateVisual();
        _repaintTimer.Start();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Updates resource values for the next repaint.</summary>
    public void UpdateResources(double hpPct, double manaPct)
    {
        _hpPct   = hpPct;
        _manaPct = manaPct;
    }

    /// <summary>Updates cooldown data for the next repaint.</summary>
    public void UpdateCooldowns(IReadOnlyDictionary<string, (double Remaining, double Total)> cooldowns)
        => _cooldowns = cooldowns;

    /// <summary>Positions the overlay over the given screen rectangle.</summary>
    public void AlignTo(System.Drawing.Rectangle gameBounds)
    {
        if (gameBounds.IsEmpty) return;
        Left   = gameBounds.Left;
        Top    = gameBounds.Top;
        Width  = gameBounds.Width;
        Height = gameBounds.Height;

        // Resize the SkiaSharp canvas to fill the overlay
        SkiaCanvas.Width  = gameBounds.Width;
        SkiaCanvas.Height = gameBounds.Height;
    }

    // ── SkiaSharp paint handler ───────────────────────────────────────────────

    private void OnPaintSurface(object sender, SKPaintSurfaceEventArgs e)
    {
        SKCanvas canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        float w = e.Info.Width;
        float h = e.Info.Height;

        DrawResourceBars(canvas, w, h);
        DrawCooldownCircles(canvas, w, h);
    }

    // ── Drawing helpers ───────────────────────────────────────────────────────

    private void DrawResourceBars(SKCanvas canvas, float w, float h)
    {
        const float barW   = 160f;
        const float barH   = 14f;
        const float margin = 10f;
        float x = w - barW - margin;

        // ── HP bar ──
        float yHp = margin;
        DrawBar(canvas, x, yHp, barW, barH, (float)(_hpPct / 100.0), HpBarColor, "HP");

        // ── Mana bar ──
        float yMana = yHp + barH + 6f;
        DrawBar(canvas, x, yMana, barW, barH, (float)(_manaPct / 100.0), ManaBarColor, "MP");
    }

    private static void DrawBar(SKCanvas canvas, float x, float y, float totalW, float barH,
                                float fraction, SKColor fill, string label)
    {
        using var bgPaint = new SKPaint
        {
            Color       = BackBarColor,
            IsAntialias = true,
            Style       = SKPaintStyle.Fill
        };
        canvas.DrawRoundRect(x, y, totalW, barH, 4, 4, bgPaint);

        if (fraction > 0f)
        {
            using var fgPaint = new SKPaint
            {
                Color       = fill,
                IsAntialias = true,
                Style       = SKPaintStyle.Fill
            };
            canvas.DrawRoundRect(x, y, totalW * fraction, barH, 4, 4, fgPaint);
        }

        using var txtPaint = new SKPaint
        {
            Color       = TextColor,
            TextSize    = 10f,
            IsAntialias = true
        };
        canvas.DrawText($"{label} {fraction * 100:F0}%", x + 4, y + barH - 2, txtPaint);
    }

    private void DrawCooldownCircles(SKCanvas canvas, float w, float h)
    {
        float cx = 50f;
        float cy = h - 60f;
        float radius = 22f;
        float spacing = 60f;

        int idx = 0;
        foreach (var (label, (remaining, total)) in _cooldowns)
        {
            float centerX = cx + idx * spacing;
            DrawCooldownArc(canvas, centerX, cy, radius, remaining, total, label);
            idx++;
        }
    }

    private static void DrawCooldownArc(SKCanvas canvas, float cx, float cy, float radius,
                                        double remaining, double total, string label)
    {
        // Background circle
        using var bgPaint = new SKPaint
        {
            Color       = CooldownBg,
            Style       = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(cx, cy, radius, bgPaint);

        // Sweep arc: full circle = total, remaining portion highlighted
        float fraction = total > 0 ? (float)(remaining / total) : 0f;
        float sweepAngle = fraction * 360f;

        using var arcPaint = new SKPaint
        {
            Color       = CooldownArc,
            Style       = SKPaintStyle.Stroke,
            StrokeWidth = 4f,
            IsAntialias = true,
            StrokeCap   = SKStrokeCap.Round
        };

        var rect = new SKRect(cx - radius, cy - radius, cx + radius, cy + radius);
        using var path = new SKPath();
        // Start at 12 o'clock (-90°)
        path.AddArc(rect, -90f, sweepAngle);
        canvas.DrawPath(path, arcPaint);

        // Label text
        using var txtPaint = new SKPaint
        {
            Color       = TextColor,
            TextSize    = 9f,
            IsAntialias = true,
            TextAlign   = SKTextAlign.Center
        };
        canvas.DrawText($"{remaining:F0}s", cx, cy + 4, txtPaint);
        canvas.DrawText(label, cx, cy + radius + 12, txtPaint);
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────
    protected override void OnClosed(EventArgs e)
    {
        _repaintTimer.Stop();
        base.OnClosed(e);
    }
}
