using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace Blitztext.Tests;

public sealed class ShortcutTests
{
    private const int Middle = DictationShortcut.MiddleButton;
    private static readonly int[] Legacy = [0x78, 0x75, 0x77, 0x76, 0x7A, 0x7B];

    private static ShortcutInput Input(DictationShortcut? binding = null)
    {
        var input = new ShortcutInput();
        input.Configure(Legacy, binding ?? new());
        return input;
    }

    [Fact]
    public void MiddlePressReleasePressRecordsThenProcessesOnce()
    {
        var fake = new SessionHarness();
        Assert.True(fake.Input.Process(Middle, true));
        fake.RunMicrophoneStart();
        Assert.True(fake.Input.Process(Middle, false));
        Assert.Equal(RecordingState.Recording, fake.Gate.State);
        Assert.Equal(1, fake.MicrophoneStarts);
        Assert.True(fake.Input.Process(Middle, true));
        Assert.True(fake.Input.Process(Middle, true));
        Assert.True(fake.Input.Process(Middle, false));
        fake.Input.Process(Middle, true); // Processing rejects another start.
        fake.Input.Process(Middle, false);
        Assert.Equal(1, fake.ProcessRequests);
        fake.FinishProcessing();
        Assert.Equal(1, fake.Deliveries);
        Assert.Equal(RecordingState.Idle, fake.Gate.State);
    }

    [Fact]
    public void VeryFastTwoClicksBeforeQueuedMicrophoneStartDiscardWithoutOpeningDevice()
    {
        var fake = new SessionHarness();
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.RunMicrophoneStart();
        fake.FinishProcessing();
        Assert.Equal(0, fake.MicrophoneStarts);
        Assert.Equal(0, fake.Deliveries);
        Assert.Equal(RecordingState.Idle, fake.Gate.State);
    }

    [Theory]
    [InlineData(0x78)] [InlineData(0x75)] [InlineData(0x77)] [InlineData(0x76)] [InlineData(0x7A)]
    public void LegacyHoldStopsOnlyOnItsOwnRelease(int key)
    {
        var fake = new SessionHarness();
        fake.Input.Process(key, true);
        fake.RunMicrophoneStart();
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.Input.Process(0x41, true);
        fake.Input.Process(0x41, false);
        Assert.Equal(RecordingState.Recording, fake.Gate.State);
        fake.Input.Process(key, false);
        Assert.Equal(1, fake.ProcessRequests);
        fake.FinishProcessing();
        Assert.Equal(1, fake.Deliveries);
    }

    [Fact]
    public void KeyboardToggleDoesNotRepeatOrStopOnRelease()
    {
        var input = Input(new() { Key = 0x44, Modifiers = ShortcutModifiers.Control | ShortcutModifiers.Shift });
        int toggles = 0;
        input.TogglePressed += _ => toggles++;
        Assert.False(input.Process(0xA2, true));
        Assert.False(input.Process(0xA1, true));
        Assert.True(input.Process(0x44, true));
        for (int i = 0; i < 20; i++) Assert.True(input.Process(0x44, true));
        Assert.True(input.Process(0x44, false));
        Assert.Equal(1, toggles);
        Assert.True(input.Process(0x44, true));
        Assert.Equal(2, toggles);
        Assert.False(input.Process(0xA2, false));
        Assert.False(input.Process(0xA1, false));
        Assert.True(input.Process(0x44, false));
    }

    [Fact]
    public void DisabledShortcutAndInjectedEventsPassThrough()
    {
        var input = Input(new() { Enabled = false });
        int actions = 0;
        input.TogglePressed += _ => actions++;
        Assert.False(input.Process(Middle, true));
        Assert.False(input.Process(Middle, false));
        input.Configure(Legacy, new());
        Assert.False(input.Process(Middle, true, injected: true));
        Assert.False(input.Process(Middle, false, injected: true));
        Assert.True(input.Process(Middle, true));
        Assert.Equal(1, actions);
        Assert.True(input.Process(Middle, false));
    }

    [Theory]
    [InlineData(0x0200, 0)] [InlineData(0x020A, 0)] [InlineData(0x020E, 0)]
    [InlineData(0x0201, 0)] [InlineData(0x0204, 0)] [InlineData(0x020B, 0)]
    [InlineData(0x0207, 1)] [InlineData(0x0208, 3)]
    public void NativeMouseFilterIgnoresMovementWheelOtherButtonsAndInjection(int message, uint flags)
        => Assert.False(MouseHook.IsPhysicalMiddle(message, flags));

    [Theory]
    [InlineData(0x0207)] [InlineData(0x0208)]
    public void NativeMouseFilterAcceptsOnlyPhysicalMiddleTransitions(int message)
        => Assert.True(MouseHook.IsPhysicalMiddle(message, 0));

    [Fact]
    public void ReconfigurationPreservesConsumedAndForwardedPressPairs()
    {
        var input = Input();
        Assert.True(input.Process(Middle, true));
        input.Configure(Legacy, new() { Enabled = false });
        Assert.True(input.Process(Middle, false));
        Assert.False(input.Process(Middle, true));
        input.Configure(Legacy, new());
        Assert.False(input.Process(Middle, true)); // Existing unconsumed repeat stays unconsumed.
        Assert.False(input.Process(Middle, false));
        Assert.True(input.Process(Middle, true));
        Assert.True(input.Process(Middle, false));
    }

    [Fact]
    public void ClosingOrdinarySettingsDoesNotLoseHeldHotkeyRelease()
    {
        var fake = new SessionHarness();
        Assert.True(fake.Input.Process(0x78, true));
        fake.RunMicrophoneStart();
        fake.Input.CancelCapture(); // Settings closes without ever entering capture mode.
        Assert.True(fake.Input.Process(0x78, false));
        Assert.Equal(RecordingState.Processing, fake.Gate.State);
        fake.FinishProcessing();
        Assert.Equal(1, fake.Deliveries);
    }

    [Fact]
    public void HeldLegacyKeyStillReleasesAfterItsBindingIsChanged()
    {
        var input = Input();
        int released = 0;
        input.LegacyReleased += key => released = key;
        Assert.True(input.Process(0x78, true));
        input.Configure([0x70], new());
        Assert.True(input.Process(0x78, false));
        Assert.Equal(0x78, released);
    }

    [Fact]
    public void ActiveRecordingRetainsOriginalToggleUntilItCompletes()
    {
        var input = Input();
        int toggles = 0;
        input.TogglePressed += binding => { input.ActiveToggle = binding; toggles++; };
        input.Process(Middle, true);
        input.Process(Middle, false);
        input.Configure(Legacy, new() { Key = 0x44 });
        Assert.False(input.Process(0x44, true));
        input.Process(0x44, false);
        Assert.True(input.Process(Middle, true));
        input.Process(Middle, false);
        Assert.Equal(2, toggles);
        input.ActiveToggle = null;
        Assert.True(input.Process(0x44, true));
        Assert.Equal(3, toggles);
    }

    [Fact]
    public void CaptureWaitsForWholeGestureAndNeverActivatesRecording()
    {
        var input = Input();
        DictationShortcut? captured = null;
        int actions = 0;
        input.TogglePressed += _ => actions++;
        input.LegacyPressed += _ => actions++;
        input.CaptureCompleted += result => captured = result;
        input.BeginCapture();
        Assert.True(input.Process(0xA2, true));
        Assert.True(input.Process(0xA0, true));
        Assert.True(input.Process(0x44, true));
        Assert.True(input.Process(0x44, false));
        Assert.Null(captured);
        Assert.True(input.Process(0xA0, false));
        Assert.True(input.Process(0xA2, false));
        Assert.NotNull(captured);
        Assert.Equal(0x44, captured.Key);
        Assert.Equal(ShortcutModifiers.Control | ShortcutModifiers.Shift, captured.Modifiers);
        Assert.False(input.IsCapturing);
        Assert.Equal(0, actions);
    }

    [Fact]
    public void CaptureMiddleAndEscapeAreConsumedUntilRelease()
    {
        var input = Input();
        var results = new List<DictationShortcut?>();
        input.CaptureCompleted += results.Add;
        input.BeginCapture();
        Assert.True(input.Process(Middle, true));
        Assert.Empty(results);
        Assert.True(input.Process(Middle, false));
        Assert.Equal(Middle, results[0]!.Key);
        input.BeginCapture();
        Assert.True(input.Process(0xA2, true));
        Assert.True(input.Process(0x1B, true));
        Assert.True(input.Process(0x1B, false));
        Assert.Single(results);
        Assert.True(input.Process(0xA2, false));
        Assert.Null(results[1]);
    }

    [Fact]
    public void ClosingCaptureDrainsConsumedKeysWithoutChangingConfiguration()
    {
        var input = Input();
        int completed = 0, toggles = 0;
        input.CaptureCompleted += _ => completed++;
        input.TogglePressed += _ => toggles++;
        input.BeginCapture();
        Assert.True(input.Process(0xA2, true));
        Assert.True(input.Process(0x44, true));
        input.CancelCapture();
        Assert.True(input.Process(0x44, false));
        Assert.True(input.Process(0xA2, false));
        Assert.Equal(0, completed);
        input.Process(Middle, true);
        Assert.Equal(1, toggles);
    }

    [Fact]
    public void AlreadyHeldKeysRetainForwardedReleaseDuringCapture()
    {
        var input = Input();
        input.SeedPressed(0xA2);
        input.BeginCapture();
        DictationShortcut? result = null;
        input.CaptureCompleted += value => result = value;
        Assert.True(input.Process(0x44, true));
        Assert.True(input.Process(0x44, false));
        Assert.False(input.Process(0xA2, false));
        Assert.Equal(ShortcutModifiers.Control, result!.Modifiers);
    }

    [Fact]
    public void EscapeInvalidatesQueuedStartAndLateProcessingResults()
    {
        var fake = new SessionHarness();
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.Input.Process(0x1B, true);
        fake.Input.Process(0x1B, false);
        fake.RunMicrophoneStart();
        Assert.Equal(0, fake.MicrophoneStarts);
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.RunMicrophoneStart();
        fake.Input.Process(Middle, true);
        fake.Input.Process(Middle, false);
        fake.Input.Process(0x1B, true);
        fake.Input.Process(0x1B, false);
        fake.Input.Process(Middle, true); // New session must survive the old completion.
        fake.Input.Process(Middle, false);
        fake.FinishProcessing();
        Assert.Equal(0, fake.Deliveries);
        Assert.Equal(RecordingState.Recording, fake.Gate.State);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MicrophoneOrProcessingFailureAllowsAnotherSession(bool processing)
    {
        var gate = new RecordingGate();
        Assert.True(gate.TryStart(RecordingGate.ToggleTrigger));
        int session = gate.Session;
        Assert.True(gate.TryBeginCapture(session));
        if (processing) Assert.True(gate.TryStop(RecordingGate.ToggleTrigger));
        gate.Complete(session); // same finally/error path used by the host
        Assert.True(gate.TryStart(0x78));
        Assert.False(gate.IsCurrent(session));
    }

    [Fact]
    public void LibraryKeyTriggersOncePerPress()
    {
        var input = Input();
        int opens = 0;
        input.LegacyPressed += key => { if (key == 0x7B) opens++; };
        for (int i = 0; i < 10; i++) input.Process(0x7B, true);
        input.Process(0x7B, false);
        Assert.Equal(1, opens);
    }

    [Theory]
    [InlineData(0x78, ShortcutModifiers.None)]
    [InlineData(0x78, ShortcutModifiers.Control)]
    [InlineData(0x1B, ShortcutModifiers.None)]
    [InlineData(1, ShortcutModifiers.None)]
    [InlineData(2, ShortcutModifiers.None)]
    [InlineData(0xA2, ShortcutModifiers.None)]
    [InlineData(0x4C, ShortcutModifiers.Windows)]
    public void ConflictingOrReservedBindingsAreRejected(int key, ShortcutModifiers modifiers)
        => Assert.NotNull(new DictationShortcut { Key = key, Modifiers = modifiers }.Validate(Legacy));

    [Fact]
    public void RightModifierLegacyBindingsCannotOverlapCombinations()
    {
        Assert.NotNull(new DictationShortcut { Key = 0x44, Modifiers = ShortcutModifiers.Control }.Validate([0xA3]));
        Assert.NotNull(new DictationShortcut { Key = Middle, Modifiers = ShortcutModifiers.Shift }.Validate([0xA1]));
        Assert.Null(new DictationShortcut().Validate(Legacy));
    }

    [Fact]
    public void OldSettingsRetainPreferencesAndGainDefaultMiddleToggle()
    {
        var options = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var settings = JsonSerializer.Deserialize<Settings>("""
            {"Mode":"Server","ServerUrl":"https://example.invalid","AutoPaste":false,
             "StrictPasteTarget":false,"ClearClipboardAfterPaste":false,"ClipboardClearSeconds":42,
             "LaunchAtLogin":true,"BlitzNoteRoot":"custom","Language":"de","Tone":"formal",
             "ImproveSystemPrompt":"keep","Hotkeys":{"Transcribe":"F2","Improve":"F3",
             "Steam":"F4","Emoji":"F7","Note":"F11","Library":"F12"}}
            """, options)!;
        var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings, options), options)!;
        Assert.Equal(ConnectionMode.Server, copy.Mode);
        Assert.False(copy.AutoPaste);
        Assert.False(copy.StrictPasteTarget);
        Assert.False(copy.ClearClipboardAfterPaste);
        Assert.Equal(42, copy.ClipboardClearSeconds);
        Assert.True(copy.LaunchAtLogin);
        Assert.Equal("custom", copy.BlitzNoteRoot);
        Assert.Equal("F2", copy.Hotkeys.Transcribe);
        Assert.Equal("F3", copy.Hotkeys.Improve);
        Assert.Equal("F11", copy.Hotkeys.Note);
        Assert.Equal("F12", copy.Hotkeys.Library);
        Assert.Equal("keep", copy.ImproveSystemPrompt);
        Assert.True(copy.DictationShortcut.Enabled);
        Assert.Equal(Middle, copy.DictationShortcut.Key);
        Assert.Equal(WorkflowKind.Transcribe, copy.DictationShortcut.Workflow);
    }

    [Fact]
    public void EditingDraftDoesNotMutateSavedSettingsAndRoundTripsWhenSaved()
    {
        var settings = new Settings();
        var draft = settings.DictationShortcut.Copy();
        draft.Key = 0x44;
        draft.Modifiers = ShortcutModifiers.Control | ShortcutModifiers.Shift;
        draft.Workflow = WorkflowKind.Improve;
        Assert.Equal(Middle, settings.DictationShortcut.Key); // closing the window discards the draft
        Assert.Null(draft.Validate(Legacy));
        settings.DictationShortcut = draft.Copy();
        var roundTrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
        Assert.Equal("Strg + Umschalt + D", roundTrip.DictationShortcut.DisplayName);
        Assert.Equal(WorkflowKind.Improve, roundTrip.DictationShortcut.Workflow);
        draft.Enabled = false;
        Assert.True(settings.DictationShortcut.Enabled);
    }

    // Fake hardware/network/delivery at the boundary of the production router and session gate.
    // These tests do not claim to exercise native hooks, NAudio, Avalonia or real clipboard delivery.
    private sealed class SessionHarness
    {
        public ShortcutInput Input { get; } = ShortcutTests.Input();
        public RecordingGate Gate { get; } = new();
        public int MicrophoneStarts, ProcessRequests, Deliveries;
        private int _queuedStart, _queuedProcess;
        private bool _processHasAudio;
        public SessionHarness()
        {
            Input.TogglePressed += binding =>
            {
                if (Gate.TryStart(RecordingGate.ToggleTrigger))
                { Input.ActiveToggle = binding; _queuedStart = Gate.Session; }
                else Stop(RecordingGate.ToggleTrigger);
            };
            Input.LegacyPressed += key => { if (Gate.TryStart(key)) _queuedStart = Gate.Session; };
            Input.LegacyReleased += Stop;
            Input.EscapePressed += () => { Gate.Cancel(); Input.ActiveToggle = null; };
        }
        private void Stop(int trigger)
        {
            if (!Gate.TryStop(trigger)) return;
            _queuedProcess = Gate.Session;
            _processHasAudio = Gate.CaptureStarted;
            ProcessRequests++;
        }
        public void RunMicrophoneStart()
        { if (Gate.TryBeginCapture(_queuedStart)) MicrophoneStarts++; }
        public void FinishProcessing()
        {
            if (Gate.IsCurrent(_queuedProcess) && _processHasAudio) Deliveries++;
            Gate.Complete(_queuedProcess);
            if (Gate.State == RecordingState.Idle) Input.ActiveToggle = null;
        }
    }
}
