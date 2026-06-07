using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace Blitztext;

public partial class App : Application
{
    private TrayViewModel? _vm;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Kein MainWindow → keine sichtbare Oberfläche beim Start. Tray-Icon ist die einzige Präsenz.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _vm = new TrayViewModel();
            DataContext = _vm;

            desktop.Exit += (_, _) => _vm?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
