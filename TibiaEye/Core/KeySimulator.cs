using System.Runtime.Versioning;
using WindowsInput;
using WindowsInput.Native;

namespace TibiaEye.Core;

/// <summary>
/// Thin wrapper around <c>InputSimulatorPlus</c> that injects a human-like
/// random delay (50–150 ms) before every key-press to reduce bot-detection risk.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class KeySimulator
{
    private readonly InputSimulator _sim = new();
    private readonly Random _rng = Random.Shared;

    private const int DelayMinMs = 50;
    private const int DelayMaxMs = 150;

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Presses a function key (F1–F12) after a randomised human delay.
    /// </summary>
    public async Task PressKeyAsync(string key, CancellationToken ct = default)
    {
        await RandomDelayAsync(ct);
        VirtualKeyCode vk = ParseKey(key);
        _sim.Keyboard.KeyPress(vk);
    }

    /// <summary>
    /// Types an in-game spell string (e.g. "exura") followed by Enter.
    /// </summary>
    public async Task TypeSpellAsync(string spell, CancellationToken ct = default)
    {
        await RandomDelayAsync(ct);
        _sim.Keyboard.TextEntry(spell);
        await RandomDelayAsync(ct);
        _sim.Keyboard.KeyPress(VirtualKeyCode.RETURN);
    }

    // ── Private ──────────────────────────────────────────────────────────────

    private async Task RandomDelayAsync(CancellationToken ct)
    {
        int ms = _rng.Next(DelayMinMs, DelayMaxMs + 1);
        await Task.Delay(ms, ct);
    }

    private static VirtualKeyCode ParseKey(string key) => key.ToUpperInvariant() switch
    {
        "F1"  => VirtualKeyCode.F1,
        "F2"  => VirtualKeyCode.F2,
        "F3"  => VirtualKeyCode.F3,
        "F4"  => VirtualKeyCode.F4,
        "F5"  => VirtualKeyCode.F5,
        "F6"  => VirtualKeyCode.F6,
        "F7"  => VirtualKeyCode.F7,
        "F8"  => VirtualKeyCode.F8,
        "F9"  => VirtualKeyCode.F9,
        "F10" => VirtualKeyCode.F10,
        "F11" => VirtualKeyCode.F11,
        "F12" => VirtualKeyCode.F12,
        _     => throw new ArgumentException($"Unsupported hotkey: '{key}'", nameof(key))
    };
}
