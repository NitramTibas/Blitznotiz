using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Blitztext;

public enum ToastKind { Info, Error, Success }

/// <summary>
/// Kleines, selbstschließendes Hinweis-Fenster unten rechts. Ersetzt die früheren Tray-Ballons:
/// macht stille Fehler (kein Schlüssel, Einfügen blockiert, Mikrofon-Fehler) für den Nutzer sichtbar.
/// Stiehlt bewusst keinen Fokus (ShowActivated=false), damit das Einfügeziel erhalten bleibt.
/// </summary>
public sealed class Toast : Window
{
    private static Toast? _current;
    private readonly DispatcherTimer _timer;

    private Toast(string message, IBrush accent)
    {
        SystemDecorations = SystemDecorations.None;
        ExtendClientAreaToDecorationsHint = true;
        // Echte Fenster-Transparenz, sonst rahmt ein opaker Block die runden Ecken (Avalonia/Windows).
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        CanResize = false;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Background = Brushes.Transparent;
        Title = AppBrand.Name;

        Content = new Border
        {
            MinWidth = 260,
            MaxWidth = 360,
            Background = Res("BtSurfaceBrush"),
            BorderBrush = Res("BtBorderBrush"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16, 12),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                Children =
                {
                    new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = accent },
                    new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                        MaxWidth = 320,
                        FontSize = 14,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Res("BtTextPrimaryBrush")
                    }
                }
            }
        };

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _timer.Tick += (_, _) => { _timer.Stop(); Close(); };
        _timer.Start();

        Opened += OnOpened;
    }

    public static void Show(string message, ToastKind kind = ToastKind.Info)
    {
        void DoShow()
        {
            _current?.Close();
            var accent = kind switch
            {
                ToastKind.Error => Res("BtRecordingBrush"),
                ToastKind.Success => Res("BtSuccessBrush"),
                _ => Res("BtAccentBrush")
            };
            var t = new Toast(message, accent);
            _current = t;
            t.Closed += (_, _) => { if (ReferenceEquals(_current, t)) _current = null; };
            t.Show();
        }

        if (Dispatcher.UIThread.CheckAccess()) DoShow();
        else Dispatcher.UIThread.Post(DoShow);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary;
        if (screen is null) return;
        var wa = screen.WorkingArea;          // Geräte-Pixel
        var scale = screen.Scaling;
        int w = (int)(ClientSize.Width * scale);
        int h = (int)(ClientSize.Height * scale);
        int margin = (int)(16 * scale);
        Position = new PixelPoint(wa.X + wa.Width - w - margin, wa.Y + wa.Height - h - margin);
    }

    private static IBrush Res(string key)
    {
        if (Application.Current is { } app && app.TryGetResource(key, ThemeVariant.Dark, out var v) && v is IBrush b)
            return b;
        return Brushes.Gray;
    }
}
