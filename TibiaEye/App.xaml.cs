using System.Windows;
using TibiaEye.Core;

namespace TibiaEye;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Ensure the scanner tries to attach on first run
        GameScanner.Instance.TryAttach();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        GameScanner.Instance.Dispose();
        base.OnExit(e);
    }
}

