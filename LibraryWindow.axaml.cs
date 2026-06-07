using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Blitztext;

/// <summary>
/// Schmales, am Bildschirmrand angedocktes Bibliotheks-Panel für Blitznotizen.
/// Tag-/Monats-Ansicht, Blättern, Klick = wiederverwenden (zurück in die Zwischenablage + grün),
/// Einzel-Löschen je Zeile, Sammel-Löschen (verwendete/alle) mit Nachfrage. Datei ist die Wahrheit:
/// jede Aktion schreibt in die Markdown-Datei und lädt danach neu.
/// </summary>
public partial class LibraryWindow : Window
{
    private static readonly string[] WeekdaysShort = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };
    private static readonly string[] MonthNames =
    {
        "Januar", "Februar", "März", "April", "Mai", "Juni",
        "Juli", "August", "September", "Oktober", "November", "Dezember"
    };

    private readonly string _root;
    private readonly string _side;
    private DateOnly _cursor;
    private bool _monthMode;

    private readonly TextBlock _rangeLabel;
    private readonly TextBlock _countTotal;
    private readonly TextBlock _countUsed;
    private readonly TextBlock _countOpen;
    private readonly StackPanel _listPanel;
    private readonly Button _tabDay;
    private readonly Button _tabMonth;

    // Parameterloser Konstruktor für den XAML-Designer; Laufzeit nutzt den mit Settings.
    public LibraryWindow() : this(Settings.Load()) { }

    public LibraryWindow(Settings settings)
    {
        InitializeComponent();

        _rangeLabel = this.FindControl<TextBlock>("RangeLabel")!;
        _countTotal = this.FindControl<TextBlock>("CountTotal")!;
        _countUsed = this.FindControl<TextBlock>("CountUsed")!;
        _countOpen = this.FindControl<TextBlock>("CountOpen")!;
        _listPanel = this.FindControl<StackPanel>("ListPanel")!;
        _tabDay = this.FindControl<Button>("TabDay")!;
        _tabMonth = this.FindControl<Button>("TabMonth")!;

        _root = BlitzNoteStore.ResolveRoot(settings);
        _side = settings.BlitzNotePanelSide == "left" ? "left" : "right";
        _cursor = DateOnly.FromDateTime(DateTime.Now);

        Opened += OnOpened;
        Reload();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>Von außen aufrufbar (z.B. nach neuer Aufnahme), aktualisiert die Ansicht.</summary>
    public void Refresh()
    {
        if (Dispatcher.UIThread.CheckAccess()) Reload();
        else Dispatcher.UIThread.Post(Reload);
    }

    // ── Andocken ─────────────────────────────────────────────────────────────────
    private void OnOpened(object? sender, EventArgs e)
    {
        var screen = Screens.Primary ?? (Screens.All.Count > 0 ? Screens.All[0] : null);
        if (screen is null) return;
        var wa = screen.WorkingArea;          // Geräte-Pixel
        var scale = screen.Scaling;

        double widthDip = Math.Clamp(Math.Round(wa.Width / scale / 3.0), 340, 560);
        double heightDip = wa.Height / scale;
        Width = widthDip;
        Height = heightDip;

        int wPix = (int)(widthDip * scale);
        int x = _side == "left" ? wa.X : wa.X + wa.Width - wPix;
        Position = new PixelPoint(x, wa.Y);
    }

    // ── Laden / Anzeige ──────────────────────────────────────────────────────────
    private void Reload()
    {
        List<NoteEntry> notes;
        if (_monthMode)
        {
            _rangeLabel.Text = $"{MonthNames[_cursor.Month - 1]} {_cursor.Year}";
            notes = BlitzNoteStore.LoadMonth(_root, _cursor.Year, _cursor.Month);
        }
        else
        {
            _rangeLabel.Text = DayLabel(_cursor);
            notes = BlitzNoteStore.LoadDay(_root, _cursor);
        }

        SetActive(_tabDay, !_monthMode);
        SetActive(_tabMonth, _monthMode);

        int used = notes.Count(n => n.Done);
        int open = notes.Count - used;
        _countTotal.Text = $"{notes.Count} {(notes.Count == 1 ? "Notiz" : "Notizen")}";
        _countUsed.Text = $"{used} wiederverwendet";
        _countOpen.Text = $"{open} offen";

        BuildList(notes);
    }

    private void BuildList(List<NoteEntry> notes)
    {
        _listPanel.Children.Clear();

        if (notes.Count == 0)
        {
            _listPanel.Children.Add(new TextBlock
            {
                Classes = { "meta" },
                Margin = new Thickness(6, 16, 6, 0),
                Text = _monthMode ? "Noch keine Notizen in diesem Monat." : "Noch keine Notizen an diesem Tag.",
            });
            return;
        }

        foreach (var note in notes)
            _listPanel.Children.Add(BuildRow(note));
    }

    private Control BuildRow(NoteEntry note)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };

        var inner = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        var time = new TextBlock
        {
            Text = _monthMode ? $"{note.Date.Day:00}. {note.Time}" : note.Time,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Foreground = note.Done ? Res("BtSuccessBrush") : Res("BtTextMutedBrush"),
        };
        var text = new TextBlock
        {
            Text = note.Text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = note.Done ? Res("BtSuccessBrush") : Res("BtTextPrimaryBrush"),
        };
        Grid.SetColumn(time, 0);
        Grid.SetColumn(text, 1);
        inner.Children.Add(time);
        inner.Children.Add(text);

        var reuse = new Button { Classes = { "note-row" }, Content = inner };
        ToolTip.SetTip(reuse, "In Zwischenablage zurücklegen");
        reuse.Click += (_, _) => _ = OnReuseAsync(note, reuse);
        Grid.SetColumn(reuse, 0);

        var del = new Button { Classes = { "note-del" }, Content = "✕", VerticalAlignment = VerticalAlignment.Center };
        ToolTip.SetTip(del, "Diese Notiz löschen");
        del.Click += (_, _) => OnDeleteOne(note);
        Grid.SetColumn(del, 1);

        grid.Children.Add(reuse);
        grid.Children.Add(del);
        return grid;
    }

    private string DayLabel(DateOnly d)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        string core = $"{WeekdaysShort[(int)d.DayOfWeek]}, {d.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}";
        if (d == today) return "Heute · " + core;
        if (d == today.AddDays(-1)) return "Gestern · " + core;
        return core;
    }

    // ── Aktionen ─────────────────────────────────────────────────────────────────
    private async Task OnReuseAsync(NoteEntry note, Button row)
    {
        row.IsEnabled = false; // Doppelklick während der Zwischenablage-Operation verhindern
        try
        {
            await Paster.TrySetClipboardAsync(note.Text);
            BlitzNoteStore.SetDone(_root, note, true);
        }
        finally { Reload(); } // baut die Liste (und damit die Zeile) frisch auf
    }

    private void OnDeleteOne(NoteEntry note)
    {
        BlitzNoteStore.Delete(_root, note);
        Reload();
    }

    private async void OnDeleteUsed(object? sender, RoutedEventArgs e) => await DeleteAsync(onlyUsed: true);
    private async void OnDeleteAll(object? sender, RoutedEventArgs e) => await DeleteAsync(onlyUsed: false);

    private async Task DeleteAsync(bool onlyUsed)
    {
        string scope = _monthMode ? $"im {MonthNames[_cursor.Month - 1]} {_cursor.Year}" : $"am {DayLabel(_cursor)}";
        string header = onlyUsed ? "Verwendete Notizen löschen?" : "Alle Notizen löschen?";
        string what = onlyUsed ? "alle bereits wiederverwendeten Notizen" : "alle Notizen";
        string body = $"Es werden {what} {scope} entfernt. Das lässt sich nicht rückgängig machen.";

        if (!await ConfirmDialog.ShowAsync(this, header, body, "Löschen")) return;

        if (_monthMode) BlitzNoteStore.DeleteMonth(_root, _cursor.Year, _cursor.Month, onlyUsed);
        else BlitzNoteStore.DeleteDay(_root, _cursor, onlyUsed);
        Reload();
    }

    // ── Navigation / Modus ───────────────────────────────────────────────────────
    private void OnPrev(object? sender, RoutedEventArgs e)
    {
        _cursor = _monthMode ? _cursor.AddMonths(-1) : _cursor.AddDays(-1);
        Reload();
    }

    private void OnNext(object? sender, RoutedEventArgs e)
    {
        _cursor = _monthMode ? _cursor.AddMonths(1) : _cursor.AddDays(1);
        Reload();
    }

    private void OnModeDay(object? sender, RoutedEventArgs e) { _monthMode = false; Reload(); }
    private void OnModeMonth(object? sender, RoutedEventArgs e) { _monthMode = true; Reload(); }

    private static void SetActive(Button b, bool active)
    {
        if (active) { if (!b.Classes.Contains("accent")) b.Classes.Add("accent"); }
        else b.Classes.Remove("accent");
    }

    // ── Fenster ──────────────────────────────────────────────────────────────────
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private static IBrush Res(string key)
    {
        if (Application.Current is { } app && app.TryGetResource(key, ThemeVariant.Dark, out var v) && v is IBrush b)
            return b;
        return Brushes.Gray;
    }
}
