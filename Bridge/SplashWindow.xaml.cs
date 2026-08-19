using System.Windows;

namespace Bridge;

/// <summary>
/// Borderless startup splash shown while migrations, DI, and the main window load.
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        VersionText.Text = Config.AssemblyVersion.ToString(3);
    }

    public void PumpFrame()
    {
        Dispatcher.Invoke(static () => { }, System.Windows.Threading.DispatcherPriority.Render);
    }
}
