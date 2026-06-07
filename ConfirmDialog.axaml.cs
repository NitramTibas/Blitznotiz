using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace Blitztext;

/// <summary>
/// Generischer Bestätigungs-Dialog im Blitztext-Design. Gibt true (Löschen/Primary) oder
/// false (Abbrechen/Esc/[X]) zurück. Genutzt für die unwiderruflichen Sammel-Löschungen der Bibliothek.
/// </summary>
public partial class ConfirmDialog : Window
{
    private readonly TextBlock _headerText;
    private readonly TextBlock _bodyText;
    private readonly Button _primaryButton;
    private readonly Button _secondaryButton;
    private bool _result;

    public ConfirmDialog()
    {
        InitializeComponent();
        _headerText = this.FindControl<TextBlock>("HeaderText")!;
        _bodyText = this.FindControl<TextBlock>("BodyText")!;
        _primaryButton = this.FindControl<Button>("PrimaryButton")!;
        _secondaryButton = this.FindControl<Button>("SecondaryButton")!;
        KeyDown += OnKey;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnKey(object? sender, KeyEventArgs e)
    {
        // Esc bricht ab. Enter ist bewusst KEINE Bestätigung: sonst löscht ein reflexhaftes Enter
        // (auch bei Fokus auf "Abbrechen") unwiderruflich. Löschen geht nur per bewusstem Klick.
        if (e.Key == Key.Escape) { _result = false; Close(); e.Handled = true; }
    }

    private void OnPrimary(object? sender, RoutedEventArgs e) { _result = true; Close(); }
    private void OnSecondary(object? sender, RoutedEventArgs e) { _result = false; Close(); }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    /// <summary>Zeigt den Dialog modal über <paramref name="owner"/>. True = Löschen bestätigt.</summary>
    public static async Task<bool> ShowAsync(Window owner, string header, string body, string primaryText)
    {
        var dlg = new ConfirmDialog();
        dlg._headerText.Text = header;
        dlg._bodyText.Text = body;
        dlg._primaryButton.Content = primaryText;
        dlg.Title = header;
        await dlg.ShowDialog(owner);
        return dlg._result;
    }
}
