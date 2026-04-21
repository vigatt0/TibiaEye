using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TibiaEye.Models;

/// <summary>
/// Observable application settings bound to the WPF dashboard controls.
/// </summary>
public sealed class AppSettings : INotifyPropertyChanged
{
    // ── Health ──────────────────────────────────────────────────────────────
    private int _hpThreshold = 70;
    /// <summary>Cast healing spell when HP % drops below this value.</summary>
    public int HpThreshold
    {
        get => _hpThreshold;
        set => SetField(ref _hpThreshold, value);
    }

    private string _hpSpellKey = "F1";
    /// <summary>Hotkey used for the healing spell.</summary>
    public string HpSpellKey
    {
        get => _hpSpellKey;
        set => SetField(ref _hpSpellKey, value);
    }

    // ── Mana ────────────────────────────────────────────────────────────────
    private int _manaThreshold = 50;
    /// <summary>Cast mana-restore when Mana % drops below this value.</summary>
    public int ManaThreshold
    {
        get => _manaThreshold;
        set => SetField(ref _manaThreshold, value);
    }

    private string _manaSpellKey = "F2";
    /// <summary>Hotkey used for mana restoration.</summary>
    public string ManaSpellKey
    {
        get => _manaSpellKey;
        set => SetField(ref _manaSpellKey, value);
    }

    // ── Feature toggles ──────────────────────────────────────────────────────
    private bool _autoHaste;
    /// <summary>Automatically cast Haste when not active.</summary>
    public bool AutoHaste
    {
        get => _autoHaste;
        set => SetField(ref _autoHaste, value);
    }

    private string _hasteSpellKey = "F3";
    public string HasteSpellKey
    {
        get => _hasteSpellKey;
        set => SetField(ref _hasteSpellKey, value);
    }

    private bool _autoManaShield;
    /// <summary>Automatically cast Utamo Vita / Mana Shield.</summary>
    public bool AutoManaShield
    {
        get => _autoManaShield;
        set => SetField(ref _autoManaShield, value);
    }

    private string _manaShieldKey = "F4";
    public string ManaShieldKey
    {
        get => _manaShieldKey;
        set => SetField(ref _manaShieldKey, value);
    }

    private bool _foodEater;
    /// <summary>Automatically eat food items.</summary>
    public bool FoodEater
    {
        get => _foodEater;
        set => SetField(ref _foodEater, value);
    }

    private string _foodKey = "F5";
    public string FoodKey
    {
        get => _foodKey;
        set => SetField(ref _foodKey, value);
    }

    // ── Scan interval ────────────────────────────────────────────────────────
    private int _scanIntervalMs = 100;
    /// <summary>How often (ms) the engine scans the game window.</summary>
    public int ScanIntervalMs
    {
        get => _scanIntervalMs;
        set => SetField(ref _scanIntervalMs, value);
    }

    // ── Overlay ──────────────────────────────────────────────────────────────
    private bool _overlayEnabled = true;
    /// <summary>Show the transparent HUD overlay over the game window.</summary>
    public bool OverlayEnabled
    {
        get => _overlayEnabled;
        set => SetField(ref _overlayEnabled, value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }
}
