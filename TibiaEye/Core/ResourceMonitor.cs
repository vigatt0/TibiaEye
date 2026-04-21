using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TibiaEye.Core;

/// <summary>
/// Analyses a captured sidebar/game-window bitmap and extracts the current
/// HP and Mana percentages by scanning horizontal pixel runs of the
/// characteristic bar colours.
/// </summary>
/// <remarks>
/// HP bar colour   : RGB(219, 79, 79)   – red-ish
/// Mana bar colour : RGB(83, 80, 218)   – blue-ish
/// A ±30-unit tolerance is applied per channel so slight anti-aliasing
/// or shader variation still produces a correct reading.
/// </remarks>
[SupportedOSPlatform("windows")]
public static class ResourceMonitor
{
    // ── Target colours ────────────────────────────────────────────────────────
    private static readonly Color HpColor   = Color.FromArgb(219,  79,  79);
    private static readonly Color ManaColor = Color.FromArgb( 83,  80, 218);
    private const int ColorTolerance = 30;

    // ── Public result record ─────────────────────────────────────────────────
    public record ResourceState(double HpPercent, double ManaPercent);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Scans <paramref name="sidebar"/> and returns HP/Mana percentages (0–100).
    /// </summary>
    public static ResourceState Analyse(Bitmap sidebar)
    {
        ArgumentNullException.ThrowIfNull(sidebar);

        // Lock the bitmap bits for fast direct access
        BitmapData data = sidebar.LockBits(
            new Rectangle(0, 0, sidebar.Width, sidebar.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            return AnalyseLocked(data, sidebar.Width, sidebar.Height);
        }
        finally
        {
            sidebar.UnlockBits(data);
        }
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private static unsafe ResourceState AnalyseLocked(BitmapData data, int width, int height)
    {
        byte* ptr = (byte*)data.Scan0.ToPointer();
        int stride = data.Stride;

        // We scan every row and record the longest horizontal run of matching
        // colour. The ratio (longest_run / width) ≈ resource percentage.
        int hpMaxRun   = 0;
        int manaMaxRun = 0;

        for (int y = 0; y < height; y++)
        {
            int hpRun   = 0;
            int manaRun = 0;

            for (int x = 0; x < width; x++)
            {
                int offset = y * stride + x * 4;
                byte b = ptr[offset];
                byte g = ptr[offset + 1];
                byte r = ptr[offset + 2];
                // offset+3 is alpha – not needed

                if (IsMatch(r, g, b, HpColor))
                    hpRun++;
                else
                    hpRun = 0;

                if (IsMatch(r, g, b, ManaColor))
                    manaRun++;
                else
                    manaRun = 0;

                if (hpRun   > hpMaxRun)   hpMaxRun   = hpRun;
                if (manaRun > manaMaxRun) manaMaxRun = manaRun;
            }
        }

        // Normalise against full sidebar width
        double hpPct   = width > 0 ? Math.Clamp((double)hpMaxRun   / width * 100.0, 0, 100) : 0;
        double manaPct = width > 0 ? Math.Clamp((double)manaMaxRun / width * 100.0, 0, 100) : 0;

        return new ResourceState(hpPct, manaPct);
    }

    private static bool IsMatch(byte r, byte g, byte b, Color target)
        => Math.Abs(r - target.R) <= ColorTolerance
        && Math.Abs(g - target.G) <= ColorTolerance
        && Math.Abs(b - target.B) <= ColorTolerance;
}
