using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TibiaEye.Core;
using TibiaEye.Models;
using TibiaEye.Overlay;

namespace TibiaEye;

[SupportedOSPlatform("windows")]
public partial class MainWindow : Window
{
    private readonly AppSettings _settings = new();
    private HealingEngine?       _engine;
    private OverlayWindow?       _overlay;

    private readonly System.Windows.Threading.DispatcherTimer _connectionTimer;

    public MainWindow()
    {
        InitializeComponent();

        HpSlider.Value   = _settings.HpThreshold;
        ManaSlider.Value = _settings.ManaThreshold;

        _connectionTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        _connectionTimer.Tick += (_, _) => RefreshConnectionStatus();
        _connectionTimer.Start();

        RefreshConnectionStatus();
    }

    private void StartButton_Click(object sender, RoutedEventArgs e)
    {
        _engine = new HealingEngine(_settings);
        _engine.ResourceUpdated  += OnResourceUpdated;
        _engine.ActionDispatched += OnActionDispatched;
        _engine.Start();

        StartButton.IsEnabled = false;
        StopButton.IsEnabled  = true;
        FooterLabel.Text      = "Engine running...";

        if (_settings.OverlayEnabled)
            ShowOverlay();
    }

    private async void StopButton_Click(object sender, RoutedEventArgs e)
    {
        if (_engine != null)
        {
            await _engine.StopAsync();
            _engine.Dispose();
            _engine = null;
        }

        _overlay?.Close();
        _overlay = null;

        StartButton.IsEnabled = true;
        StopButton.IsEnabled  = false;
        FooterLabel.Text      = "Engine stopped.";
    }

    private void HpSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int val = (int)e.NewValue;
        _settings.HpThreshold = val;
        if (HpThresholdLabel != null)
            HpThresholdLabel.Text = $"{val} %";
    }

    private void ManaSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        int val = (int)e.NewValue;
        _settings.ManaThreshold = val;
        if (ManaThresholdLabel != null)
            ManaThresholdLabel.Text = $"{val} %";
    }

    private void Feature_Changed(object sender, RoutedEventArgs e)
    {
        _settings.AutoHaste      = AutoHasteCheck.IsChecked      == true;
        _settings.AutoManaShield = AutoManaShieldCheck.IsChecked == true;
        _settings.FoodEater      = FoodEaterCheck.IsChecked      == true;
    }

    private void Overlay_Changed(object sender, RoutedEventArgs e)
    {
        _settings.OverlayEnabled = OverlayCheck.IsChecked == true;
        if (!_settings.OverlayEnabled)
        {
            _overlay?.Close();
            _overlay = null;
        }
        else if (_engine != null)
        {
            ShowOverlay();
        }
    }

    private void OnResourceUpdated(double hp, double mana)
    {
        Dispatcher.InvokeAsync(() =>
        {
            LiveHpLabel.Text      = $"{hp:F0} %";
            LiveManaLabel.Text    = $"{mana:F0} %";
            HpProgressBar.Value   = hp;
            ManaProgressBar.Value = mana;

            HpProgressBar.Foreground = hp < _settings.HpThreshold
                ? Brushes.OrangeRed
                : new SolidColorBrush(Color.FromRgb(0xDB, 0x4F, 0x4F));

            _overlay?.UpdateResources(hp, mana);

            if (_engine != null)
            {
                _overlay?.UpdateCooldowns(_engine.Cooldowns);
                AlignOverlay();
            }
        });
    }

    private void OnActionDispatched(string action)
    {
        Dispatcher.InvokeAsync(() =>
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {action}";
            ActionLog.Items.Insert(0, entry);
            if (ActionLog.Items.Count > 50)
                ActionLog.Items.RemoveAt(ActionLog.Items.Count - 1);
        });
    }

    private void ShowOverlay()
    {
        if (_overlay != null) return;
        _overlay = new OverlayWindow();
        AlignOverlay();
        _overlay.Show();
    }

    private void AlignOverlay()
    {
        if (_overlay == null) return;
        var bounds = GameScanner.Instance.GetWindowBounds();
        if (!bounds.IsEmpty)
            _overlay.AlignTo(bounds);
    }

    private void RefreshConnectionStatus()
    {
        bool attached = GameScanner.Instance.IsAttached
                     || GameScanner.Instance.TryAttach();

        StatusDot.Foreground = attached ? Brushes.LimeGreen : Brushes.Gray;
        StatusLabel.Text     = attached ? "Connected" : "Disconnected";

        if (!attached && _engine == null)
            FooterLabel.Text = "Waiting for Tibia.exe...";
    }

    protected override async void OnClosed(EventArgs e)
    {
        _connectionTimer.Stop();

        if (_engine != null)
        {
            await _engine.StopAsync();
            _engine.Dispose();
        }

        _overlay?.Close();
        base.OnClosed(e);
    }
}
