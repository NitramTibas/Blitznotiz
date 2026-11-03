using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Blitztext;

/// <summary>
/// Tray-Präsenz + Diktat-Orchestrierung von Blitztext (auf Avalonia portiert).
/// Push-to-talk: Taste halten = aufnehmen, loslassen = transkribieren → optional umschreiben → einfügen.
/// Der Tastatur-Hook-Callback läuft auf dem UI-Thread; schwere Arbeit wird per Dispatcher nachgelagert.
/// </summary>
internal sealed class TrayViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TrayAnimator _animator;
    private Settings _settings;
    private readonly KeyboardHook _hook;
    private readonly MouseHook? _mouseHook;
    private readonly ShortcutInput _input = new();
    private readonly RecordingGate _recording = new();
    private Recorder? _recorder;
    private bool _disposed;
    // Werden bei RebuildHotkeyMap als ganze, neue Instanzen ausgetauscht (atomarer Referenz-Swap),
    // statt in-place geleert/befüllt – so sieht der Hook-Thread immer einen konsistenten Stand.
    private Dictionary<int, WorkflowKind> _map = new();
    private int _noteVk = -1;      // Blitznotiz aufnehmen (Push-to-talk, legt nur ab)
    private int _libraryVk = -1;   // Bibliothek auf/zu (Toggle, kein Halten)

    private SettingsWindow? _settingsWindow;
    private LibraryWindow? _libraryWindow;
    private WindowIcon _trayIcon;
    private string _trayTooltip = AppBrand.Name;

    private WorkflowKind _activeKind;
    private bool _activeIsNote;
    private IntPtr _targetWindow;
    private CancellationTokenSource? _cts;
    private bool _waitForModifiers;

    public TrayViewModel()
    {
        Recorder.SweepOldTempFiles();
        _settings = Settings.Load();

        _animator = new TrayAnimator(SetTrayIcon, reduceMotion: false);
        _trayIcon = _animator.InitialIcon;

        OpenSettingsCommand = new RelayCommand(OpenSettings);
        QuitCommand = new RelayCommand(Quit);

        RebuildHotkeyMap();

        _input.LegacyPressed += OnKeyPressed;
        _input.LegacyReleased += OnKeyReleased;
        _input.TogglePressed += OnTogglePressed;
        _input.EscapePressed += HandleEscape;
        // Seed only genuinely held keys. Their releases must pass through (their downs preceded our hook).
        for (int vk = 1; vk <= 0xFE; vk++)
            if (vk != 1 && vk != 2 && vk != 5 && vk != 6 && vk != 0x10 && vk != 0x11 && vk != 0x12 &&
                Paster.IsKeyPressed(vk)) _input.SeedPressed(vk);
        _hook = new KeyboardHook { ProcessInput = (key, down) => _input.Process(key, down) };
        try { _mouseHook = new MouseHook((key, down) => _input.Process(key, down)); }
        catch (Exception ex)
        {
            Logger.Log(ex.Message);
            Dispatcher.UIThread.Post(() => Toast.Show(ex.Message + " Tastatur-Hotkeys bleiben verfügbar.", ToastKind.Error));
        }

        Logger.Log($"Blitztext gestartet (Avalonia). Transkription={_settings.TranscriptionModel}, AutoPaste={_settings.AutoPaste}.");

        // Erststart-Hilfe: ohne hinterlegten Zugang sähe der Nutzer beim Tastendruck nichts.
        if (!IsConfigured())
            Dispatcher.UIThread.Post(OpenSettings);
    }

    private bool IsConfigured()
    {
        if (_settings.Mode == ConnectionMode.Server)
            return !string.IsNullOrWhiteSpace(_settings.ServerUrl)
                && !string.IsNullOrWhiteSpace(SecretStore.Load(SecretStore.AppPassword));
        return !string.IsNullOrWhiteSpace(SecretStore.Load(SecretStore.OpenAiKey));
    }

    // ── Bindings ────────────────────────────────────────────────────────────────
    public WindowIcon TrayIcon { get => _trayIcon; private set { _trayIcon = value; OnPropertyChanged(nameof(TrayIcon)); } }
    public string TrayTooltip { get => _trayTooltip; private set { _trayTooltip = value; OnPropertyChanged(nameof(TrayTooltip)); } }
    public ICommand OpenSettingsCommand { get; }
    public ICommand QuitCommand { get; }

    private void SetTrayIcon(WindowIcon icon) => TrayIcon = icon;

    // ── Hotkeys ─────────────────────────────────────────────────────────────────
    private void RebuildHotkeyMap()
    {
        var map = new Dictionary<int, WorkflowKind>();
        var swallow = new HashSet<int>();
        int noteVk = -1, libraryVk = -1;

        void Add(string name, WorkflowKind kind)
        {
            if (KeyNames.ToVk(name) is int vk) { map[vk] = kind; swallow.Add(vk); }
        }

        _settings.Hotkeys ??= new HotkeyConfig();
        Add(_settings.Hotkeys.Transcribe, WorkflowKind.Transcribe);
        Add(_settings.Hotkeys.Improve, WorkflowKind.Improve);
        Add(_settings.Hotkeys.Steam, WorkflowKind.Steam);
        Add(_settings.Hotkeys.Emoji, WorkflowKind.Emoji);

        // Blitznotiz nutzt denselben Aufnahme-Weg, aber den gewählten Modus (verbessert/wörtlich);
        // das Ergebnis wird nur abgelegt statt eingefügt (siehe StopAndProcessAsync).
        var noteKind = _settings.BlitzNoteImproved ? WorkflowKind.Improve : WorkflowKind.Transcribe;
        if (KeyNames.ToVk(_settings.Hotkeys.Note) is int nvk) { map[nvk] = noteKind; swallow.Add(nvk); noteVk = nvk; }
        if (KeyNames.ToVk(_settings.Hotkeys.Library) is int lvk) { swallow.Add(lvk); libraryVk = lvk; }

        // Atomar austauschen – der Tastatur-Hook liest stets eine vollständige Instanz.
        _map = map;
        _input.Configure(swallow, _settings.DictationShortcut);
        _noteVk = noteVk;
        _libraryVk = libraryVk;
    }

    // ── Tasten (Hook-Callback läuft auf dem UI-Thread) ──────────────────────────
    private void OnKeyPressed(int key)
    {
        if (_disposed || _recording.State != RecordingState.Idle) return;
        if (key == _libraryVk) { Dispatcher.UIThread.Post(() => { if (!_disposed) ToggleLibrary(); }); return; }
        if (!_map.TryGetValue(key, out var kind)) return;
        BeginRecording(key, kind, key == _noteVk, false);
    }

    private void OnTogglePressed(DictationShortcut binding)
    {
        if (_disposed) return;
        if (_recording.State == RecordingState.Idle)
        {
            _input.ActiveToggle = binding.Copy();
            BeginRecording(RecordingGate.ToggleTrigger, binding.Workflow, false,
                binding.Key != DictationShortcut.MiddleButton || binding.Modifiers != ShortcutModifiers.None);
        }
        else StopRecording(RecordingGate.ToggleTrigger);
    }

    private void BeginRecording(int trigger, WorkflowKind kind, bool isNote, bool waitForModifiers)
    {
        if (!_recording.TryStart(trigger)) return;
        _activeKind = kind;
        _activeIsNote = isNote;
        _waitForModifiers = waitForModifiers;
        _targetWindow = Paster.GetActiveWindow();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();
        int session = _recording.Session;
        Dispatcher.UIThread.Post(() => { if (!_disposed) { UpdateUi(); StartCapture(session); } });
    }

    private void OnKeyReleased(int key) => StopRecording(key);

    private void StopRecording(int trigger)
    {
        if (!_disposed && _recording.TryStop(trigger))
        {
            int session = _recording.Session;
            var kind = _activeKind;
            var isNote = _activeIsNote;
            var target = _targetWindow;
            var waitForModifiers = _waitForModifiers;
            var recorder = _recorder;
            var token = _cts?.Token ?? CancellationToken.None;
            Dispatcher.UIThread.Post(() =>
            {
                if (_disposed || !_recording.IsCurrent(session)) return;
                UpdateUi();
                _ = StopAndProcessAsync(session, kind, target, isNote, recorder, token, waitForModifiers);
            });
        }
    }

    private void StartCapture(int session)
    {
        if (_disposed || !_recording.TryBeginCapture(session)) return;
        try
        {
            // Each session owns its recorder; a late NAudio callback can never close the next one.
            _recorder = new Recorder();
            _recorder.Start();
            Logger.Log($"Aufnahme gestartet (Modus {_activeKind}, Ziel-Fenster {_targetWindow}).");
            WarmUpConnection();
        }
        catch (Exception ex)
        {
            _recorder?.Dispose();
            _recorder = null;
            ResetIdle(session);
            Logger.Log("Mikrofon-Fehler: " + ex.Message);
            Toast.Show("Mikrofon-Fehler: " + ex.Message, ToastKind.Error);
        }
    }

    private void WarmUpConnection()
    {
        try
        {
            var (baseUrl, token, error) = ResolveConnection(_settings);
            if (error == null) new OpenAiClient(baseUrl, token).WarmUp();
        }
        catch { /* Vorwärmen ist optional */ }
    }

    private void HandleEscape()
    {
        if (_disposed || _recording.State == RecordingState.Idle) return;
        _recording.Cancel(); // invalidate queued starts and late results before returning from the hook
        _input.ActiveToggle = null;
        var recorder = _recorder;
        var cts = _cts;
        _recorder = null;
        _cts = null;
        Dispatcher.UIThread.Post(() =>
        {
            cts?.Cancel();
            recorder?.Cancel();
            recorder?.Dispose();
            cts?.Dispose();
            if (!_disposed) UpdateUi();
        });
    }

    private async Task StopAndProcessAsync(int mySession, WorkflowKind kind, IntPtr targetWindow, bool isNote,
        Recorder? recorder, CancellationToken token, bool waitForModifiers)
    {
        var settings = _settings; // ein Diktat arbeitet konsistent gegen EINEN Settings-Stand (verhindert Misch-Zustand, falls währenddessen gespeichert wird)
        var sw = Stopwatch.StartNew();
        string? capturedPath = null;
        try
        {
            // A down/up/down sequence may finish before the queued microphone start runs.
            if (recorder == null || IsStale(mySession, token)) return;
            var (path, seconds) = await recorder.StopAsync();
            capturedPath = path;
            if (IsStale(mySession, token)) return;
            var bytes = !string.IsNullOrEmpty(path) && File.Exists(path) ? new FileInfo(path).Length : 0;
            Logger.Log($"Aufnahme gestoppt: {seconds:0.00}s, {bytes} Bytes, t={sw.ElapsedMilliseconds}ms.");

            if (string.IsNullOrEmpty(path) || seconds < 0.3) { Logger.Log("Verworfen: zu kurz/keine Datei."); return; }

            var (baseUrl, tokenValue, configError) = ResolveConnection(settings);
            if (configError != null) { Logger.Log("Konfig: " + configError); Toast.Show(configError, ToastKind.Error); return; }

            var client = new OpenAiClient(baseUrl, tokenValue);
            string? whisperPrompt = seconds >= 0.9 ? Workflows.TranscriptionPrompt(settings) : null;
            var t0 = sw.ElapsedMilliseconds;
            string transcript = await client.TranscribeAsync(path, settings.TranscriptionModel, whisperPrompt, settings.Language, token);
            if (IsStale(mySession, token)) return;
            Logger.Log($"Transkription erhalten: {transcript?.Trim().Length ?? 0} Zeichen, api={sw.ElapsedMilliseconds - t0}ms, t={sw.ElapsedMilliseconds}ms.");

            if (string.IsNullOrWhiteSpace(transcript) || IsLikelyArtifact(transcript, seconds)) { Logger.Log("Verworfen: leer/Artefakt."); return; }

            string result = transcript;
            if (Workflows.NeedsRewrite(kind))
            {
                double temperature = Workflows.RewriteTemperature(kind);
                string model = string.IsNullOrWhiteSpace(settings.RewriteModel) ? Workflows.FastEditModel : settings.RewriteModel.Trim();
                string systemPrompt = Workflows.BuildSystemPrompt(kind, settings);
                t0 = sw.ElapsedMilliseconds;
                result = await client.ChatAsync(model, systemPrompt, transcript, temperature, token);
                if (IsStale(mySession, token)) return;
                Logger.Log($"Rewrite fertig: {result?.Trim().Length ?? 0} Zeichen, api={sw.ElapsedMilliseconds - t0}ms, t={sw.ElapsedMilliseconds}ms.");
            }

            if (string.IsNullOrWhiteSpace(result)) return;
            if (isNote)
                await DeliverBlitzNoteAsync(result.Trim(), mySession, settings);
            else
                await DeliverResultAsync(result.Trim(), targetWindow, mySession, settings, waitForModifiers);
        }
        catch (OperationCanceledException) { Logger.Log("Abgebrochen (Esc/Timeout)."); }
        catch (OpenAiException ex) { Logger.Log("OpenAI-Fehler: " + ex.Message); }
        catch (Exception ex) { Logger.Log("Unerwarteter Fehler: " + ex.Message); }
        finally
        {
            recorder?.Dispose();
            if (ReferenceEquals(_recorder, recorder)) _recorder = null;
            if (!string.IsNullOrEmpty(capturedPath)) { try { File.Delete(capturedPath); } catch { } }
            ResetIdle(mySession);
        }
    }

    private async Task DeliverResultAsync(string text, IntPtr targetWindow, int session, Settings settings, bool waitForModifiers)
    {
        if (!_recording.IsCurrent(session) || _disposed) return;
        if (!await Paster.TrySetClipboardAsync(text)) { Logger.Log("Zwischenablage fehlgeschlagen."); Toast.Show("Zwischenablage fehlgeschlagen.", ToastKind.Error); return; }
        Logger.Log($"Zwischenablage gesetzt ({text.Length} Zeichen), AutoPaste={settings.AutoPaste}");
        if (!_recording.IsCurrent(session) || _disposed) { _ = Paster.ClearIfUnchangedAsync(text, TimeSpan.Zero); return; }
        if (!settings.AutoPaste) return;

        if (waitForModifiers)
        {
            for (int attempt = 0; attempt < 80 && Paster.AreModifiersPressed(); attempt++)
            {
                await Task.Delay(25);
                if (!_recording.IsCurrent(session) || _disposed) return;
            }
            if (Paster.AreModifiersPressed())
            {
                Toast.Show("Text liegt in der Zwischenablage – zum Einfügen zuerst die Zusatztasten loslassen.", ToastKind.Info);
                return;
            }
        }

        bool refocused = Paster.RefocusIfNeeded(targetWindow);
        await Task.Delay(60); // dem Zielfenster kurz Zeit geben, den Fokus zu übernehmen
        if (!_recording.IsCurrent(session) || _disposed) return;
        IntPtr fg = Paster.GetActiveWindow();

        if (settings.StrictPasteTarget && targetWindow != IntPtr.Zero && fg != targetWindow)
        {
            Logger.Log($"Einfügen blockiert: Ziel={targetWindow}, refokussiert={refocused}, Vordergrund={fg}.");
            Toast.Show("Text liegt in der Zwischenablage – Einfügen ins Zielfenster wurde blockiert.", ToastKind.Info);
            return;
        }

        uint sent = Paster.SendCtrlV();
        Logger.Log($"Einfügen: Ziel={targetWindow}, refokussiert={refocused}, FensterBeimEinfügen={fg}, SendInput={sent}");
        if (sent != 4) return;

        if (settings.ClearClipboardAfterPaste && settings.ClipboardClearSeconds > 0)
            _ = Paster.ClearIfUnchangedAsync(text, TimeSpan.FromSeconds(settings.ClipboardClearSeconds));
    }

    // ── Blitznotiz: nur ablegen (Datei + Zwischenablage), KEIN Einfügen ins aktive Fenster ──
    private async Task DeliverBlitzNoteAsync(string text, int session, Settings settings)
    {
        if (!_recording.IsCurrent(session) || _disposed) return;

        bool savedToFile = false;
        try { BlitzNoteStore.Append(settings, DateTime.Now, text); savedToFile = true; }
        catch (Exception ex) { Logger.Log("Blitznotiz speichern fehlgeschlagen: " + ex.Message); }

        bool clip = await Paster.TrySetClipboardAsync(text);

        if (savedToFile)
        {
            Toast.Show("Notiz abgelegt – liegt auch in der Zwischenablage.", ToastKind.Success);
            _libraryWindow?.Refresh();
        }
        else if (clip)
            Toast.Show("Notiz nicht gespeichert – liegt aber in der Zwischenablage.", ToastKind.Error);
        else
            Toast.Show("Notiz konnte nicht abgelegt werden.", ToastKind.Error);

        Logger.Log($"Blitznotiz: Datei={savedToFile}, Zwischenablage={clip}, {text.Length} Zeichen.");
    }

    private void ToggleLibrary()
    {
        if (_libraryWindow != null) { _libraryWindow.Close(); return; }
        var win = new LibraryWindow(_settings);
        _libraryWindow = win;
        win.Closed += (_, _) => { if (ReferenceEquals(_libraryWindow, win)) _libraryWindow = null; };
        win.Show();
    }

    private (string baseUrl, string token, string? error) ResolveConnection(Settings settings)
    {
        if (settings.Mode == ConnectionMode.Server)
        {
            if (string.IsNullOrWhiteSpace(settings.ServerUrl)) return ("", "", "Kein Serverlink hinterlegt.");
            var pw = SecretStore.Load(SecretStore.AppPassword);
            if (string.IsNullOrWhiteSpace(pw)) return ("", "", "Kein App-Passwort hinterlegt.");
            return (settings.ServerUrl, pw, null);
        }
        var key = SecretStore.Load(SecretStore.OpenAiKey);
        if (string.IsNullOrWhiteSpace(key)) return ("", "", "Kein OpenAI-Key hinterlegt.");
        return ("https://api.openai.com", key, null);
    }

    private static bool IsLikelyArtifact(string text, double seconds)
    {
        string trimmed = text.Trim();
        int letters = 0;
        foreach (var c in trimmed) if (char.IsLetter(c)) letters++;
        if (letters == 0) return true;
        int chars = trimmed.Length;
        int words = trimmed.Split(new[] { ' ', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
        if (seconds < 0.55 && (words >= 5 || chars >= 32)) return true;
        if (seconds < 0.8 && chars >= 56) return true;
        return false;
    }

    private void ResetIdle(int session)
    {
        if (session != _recording.Session || _disposed) return;
        _recording.Complete(session);
        _input.ActiveToggle = null;
        UpdateUi();
    }

    private void UpdateUi()
    {
        switch (_recording.State)
        {
            case RecordingState.Recording:
                _animator.SetState(TrayVisual.Recording);
                TrayTooltip = $"{AppBrand.Name} – Aufnahme…";
                break;
            case RecordingState.Processing:
                _animator.SetState(TrayVisual.Processing);
                TrayTooltip = $"{AppBrand.Name} – verarbeite…";
                break;
            default:
                _animator.SetState(TrayVisual.Idle);
                TrayTooltip = AppBrand.Name;
                break;
        }
    }

    private bool IsStale(int session, CancellationToken token) => token.IsCancellationRequested || !_recording.IsCurrent(session) || _disposed;

    // ── Menü ────────────────────────────────────────────────────────────────────
    private void OpenSettings()
    {
        if (_settingsWindow != null) { _settingsWindow.Activate(); return; }
        var win = new SettingsWindow(_settings);
        _settingsWindow = win;
        win.CaptureShortcutRequested += () =>
        {
            HandleEscape();
            _input.BeginCapture();
        };
        void CaptureCompleted(DictationShortcut? binding) => Dispatcher.UIThread.Post(() =>
        {
            if (ReferenceEquals(_settingsWindow, win)) win.CompleteShortcutCapture(binding);
        });
        _input.CaptureCompleted += CaptureCompleted;
        win.Closed += (_, _) =>
        {
            _input.CancelCapture();
            _input.CaptureCompleted -= CaptureCompleted;
            _settingsWindow = null;
            if (!win.Saved) return;
            _settings = Settings.Load();   // frische Werte (inkl. Geheimnisse separat über SecretStore)
            RebuildHotkeyMap();            // neue Hotkeys sofort aktiv, ohne Neustart
            _libraryWindow?.Close();       // öffnet beim nächsten Hotkey mit neuem Ordner/neuer Seite
            Logger.Log($"Einstellungen übernommen. Transkription={_settings.TranscriptionModel}, " +
                       $"Hotkeys {_settings.Hotkeys.Transcribe}/{_settings.Hotkeys.Improve}/" +
                       $"{_settings.Hotkeys.Steam}/{_settings.Hotkeys.Emoji}, " +
                       $"Notiz {_settings.Hotkeys.Note}/Bibliothek {_settings.Hotkeys.Library}.");
        };
        win.Show();
    }

    private void Quit()
    {
        Logger.Flush();
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    public void Dispose()
    {
        _disposed = true;
        _recording.Cancel();
        _input.CancelCapture();
        try { _mouseHook?.Dispose(); } catch { }
        try { _hook.Dispose(); } catch { }
        try { _cts?.Cancel(); } catch { }
        try { _recorder?.Cancel(); _recorder?.Dispose(); } catch { }
        try { _cts?.Dispose(); } catch { }
        _animator.Dispose();
        Logger.Flush();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
