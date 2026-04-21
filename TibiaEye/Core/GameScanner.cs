using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace TibiaEye.Core;

/// <summary>
/// Singleton that locates the Tibia process and captures the Game Window
/// and Sidebar as <see cref="Bitmap"/> instances using the Win32 PrintWindow API.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GameScanner : IDisposable
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    private static readonly Lazy<GameScanner> _instance =
        new(() => new GameScanner(), LazyThreadSafetyMode.ExecutionAndPublication);

    public static GameScanner Instance => _instance.Value;

    // ── Win32 ────────────────────────────────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr FindWindowEx(
        IntPtr hwndParent, IntPtr hwndChildAfter,
        string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width  => Right  - Left;
        public int Height => Bottom - Top;
    }

    private const uint PW_RENDERFULLCONTENT = 0x00000002;

    // ── State ────────────────────────────────────────────────────────────────
    private IntPtr _tibiaHwnd = IntPtr.Zero;
    private readonly object _lock = new();

    private GameScanner() { }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// True when a Tibia process / window has been located.
    /// </summary>
    public bool IsAttached => _tibiaHwnd != IntPtr.Zero && IsWindow(_tibiaHwnd);

    /// <summary>
    /// Attempts to find the Tibia main window.
    /// Returns <c>true</c> on success.
    /// </summary>
    public bool TryAttach()
    {
        var proc = Process.GetProcessesByName("Tibia").FirstOrDefault()
                ?? Process.GetProcessesByName("client").FirstOrDefault();   // flash client fallback

        if (proc?.MainWindowHandle is IntPtr h && h != IntPtr.Zero)
        {
            lock (_lock) _tibiaHwnd = h;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Captures the full Tibia window as a <see cref="Bitmap"/>.
    /// Returns <c>null</c> when Tibia is not attached or the window is invalid.
    /// </summary>
    public Bitmap? CaptureGameWindow()
    {
        lock (_lock)
        {
            if (!IsAttached) return null;
            return CaptureWindow(_tibiaHwnd);
        }
    }

    /// <summary>
    /// Captures only the sidebar strip (right ~180 px of the game window),
    /// which contains HP/Mana bars and status icons.
    /// </summary>
    public Bitmap? CaptureSidebar()
    {
        var full = CaptureGameWindow();
        if (full == null) return null;

        // Sidebar is the rightmost ~180 pixels
        const int sidebarWidth = 180;
        int x = Math.Max(0, full.Width - sidebarWidth);
        var rect = new Rectangle(x, 0, full.Width - x, full.Height);

        var sidebar = full.Clone(rect, full.PixelFormat);
        full.Dispose();
        return sidebar;
    }

    /// <summary>
    /// Returns the bounding rectangle of the attached game window in screen coordinates.
    /// </summary>
    public Rectangle GetWindowBounds()
    {
        lock (_lock)
        {
            if (!IsAttached) return Rectangle.Empty;
            GetWindowRect(_tibiaHwnd, out RECT r);
            return new Rectangle(r.Left, r.Top, r.Width, r.Height);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static Bitmap? CaptureWindow(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out RECT r) || r.Width <= 0 || r.Height <= 0)
            return null;

        var bmp = new Bitmap(r.Width, r.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        IntPtr hdc = g.GetHdc();
        bool ok = PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT);
        g.ReleaseHdc(hdc);

        if (!ok)
        {
            bmp.Dispose();
            return null;
        }
        return bmp;
    }

    public void Dispose()
    {
        lock (_lock) _tibiaHwnd = IntPtr.Zero;
    }
}
