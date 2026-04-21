using System.Runtime.Versioning;
using TibiaEye.Models;

namespace TibiaEye.Core;

/// <summary>
/// Runs the main automation loop on a background <see cref="Task"/>.
/// Monitors HP/Mana and dispatches healing/spell keys accordingly.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class HealingEngine : IDisposable
{
    private readonly AppSettings _settings;
    private readonly KeySimulator _keys = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;

    // Food timer – eat roughly every 60 s
    private DateTime _lastFoodTime = DateTime.MinValue;
    private const int FoodIntervalSeconds = 60;

    // Haste / shield cooldown – avoid re-casting within 30 s
    private DateTime _lastHasteTime   = DateTime.MinValue;
    private DateTime _lastShieldTime  = DateTime.MinValue;
    private const int HasteIntervalSeconds  = 30;
    private const int ShieldIntervalSeconds = 30;

    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised on every scan tick with current resource percentages.</summary>
    public event Action<double, double>? ResourceUpdated;

    /// <summary>Raised when a spell/key is about to be pressed.</summary>
    public event Action<string>? ActionDispatched;

    // ── Cooldown tracking for overlay ─────────────────────────────────────────
    /// <summary>
    /// Remaining seconds for any active cooldown, published for the overlay.
    /// Key = spell label, Value = (remainingSeconds, totalSeconds).
    /// </summary>
    public IReadOnlyDictionary<string, (double Remaining, double Total)> Cooldowns
        => _cooldowns;

    private readonly Dictionary<string, (double Remaining, double Total)> _cooldowns = new();

    // ── Constructor ───────────────────────────────────────────────────────────
    public HealingEngine(AppSettings settings)
        => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Starts the background scan loop.</summary>
    public void Start()
    {
        if (_loopTask != null && !_loopTask.IsCompleted)
            return;

        _cts = new CancellationTokenSource();
        _loopTask = Task.Run(() => LoopAsync(_cts.Token), _cts.Token);
    }

    /// <summary>Signals the loop to stop and awaits its completion.</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_loopTask != null)
        {
            try { await _loopTask; }
            catch (OperationCanceledException) { }
        }
        _loopTask = null;
    }

    // ── Loop ─────────────────────────────────────────────────────────────────

    private async Task LoopAsync(CancellationToken ct)
    {
        var scanner = GameScanner.Instance;

        // Try to attach; retry every 2 s until found
        while (!scanner.IsAttached && !ct.IsCancellationRequested)
        {
            scanner.TryAttach();
            await Task.Delay(2000, ct);
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var sidebar = scanner.CaptureSidebar();
                if (sidebar != null)
                {
                    var state = ResourceMonitor.Analyse(sidebar);
                    ResourceUpdated?.Invoke(state.HpPercent, state.ManaPercent);

                    await HandleHealingAsync(state, ct);
                    await HandleManaAsync(state, ct);
                    await HandleHasteAsync(ct);
                    await HandleManaShieldAsync(ct);
                    await HandleFoodAsync(ct);
                }

                // Re-check attachment
                if (!scanner.IsAttached)
                    scanner.TryAttach();
            }
            catch (OperationCanceledException) { throw; }
            catch { /* swallow transient errors – scanner may be re-attaching */ }

            await Task.Delay(_settings.ScanIntervalMs, ct);
        }
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private async Task HandleHealingAsync(ResourceMonitor.ResourceState state, CancellationToken ct)
    {
        if (state.HpPercent < _settings.HpThreshold)
        {
            ActionDispatched?.Invoke($"Heal HP ({state.HpPercent:F0}%)");
            await _keys.PressKeyAsync(_settings.HpSpellKey, ct);
        }
    }

    private async Task HandleManaAsync(ResourceMonitor.ResourceState state, CancellationToken ct)
    {
        if (state.ManaPercent < _settings.ManaThreshold)
        {
            ActionDispatched?.Invoke($"Restore Mana ({state.ManaPercent:F0}%)");
            await _keys.PressKeyAsync(_settings.ManaSpellKey, ct);
        }
    }

    private async Task HandleHasteAsync(CancellationToken ct)
    {
        if (!_settings.AutoHaste) return;
        double elapsed = (DateTime.UtcNow - _lastHasteTime).TotalSeconds;
        UpdateCooldown("Haste", elapsed, HasteIntervalSeconds);

        if (elapsed >= HasteIntervalSeconds)
        {
            _lastHasteTime = DateTime.UtcNow;
            ActionDispatched?.Invoke("Auto-Haste");
            await _keys.PressKeyAsync(_settings.HasteSpellKey, ct);
        }
    }

    private async Task HandleManaShieldAsync(CancellationToken ct)
    {
        if (!_settings.AutoManaShield) return;
        double elapsed = (DateTime.UtcNow - _lastShieldTime).TotalSeconds;
        UpdateCooldown("Mana Shield", elapsed, ShieldIntervalSeconds);

        if (elapsed >= ShieldIntervalSeconds)
        {
            _lastShieldTime = DateTime.UtcNow;
            ActionDispatched?.Invoke("Auto-Mana-Shield");
            await _keys.PressKeyAsync(_settings.ManaShieldKey, ct);
        }
    }

    private async Task HandleFoodAsync(CancellationToken ct)
    {
        if (!_settings.FoodEater) return;
        double elapsed = (DateTime.UtcNow - _lastFoodTime).TotalSeconds;

        if (elapsed >= FoodIntervalSeconds)
        {
            _lastFoodTime = DateTime.UtcNow;
            ActionDispatched?.Invoke("Eat Food");
            await _keys.PressKeyAsync(_settings.FoodKey, ct);
        }
    }

    private void UpdateCooldown(string label, double elapsed, double total)
    {
        double remaining = Math.Max(0, total - elapsed);
        _cooldowns[label] = (remaining, total);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────
    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
    }
}
