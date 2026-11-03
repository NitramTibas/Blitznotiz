# Additional dictation shortcut

The fork adds an independent toggle binding, defaulting to physical middle mouse and Transcribe.
Legacy hotkeys remain separate hold bindings; Note still uses its existing storage path, and Library remains a separate toggle.

## Code and boundaries

- `DictationShortcut.cs`: backward-compatible settings value, normalized modifier flags, display names, conflict/reserved-key validation and copy-on-edit.
- `ShortcutInput.cs`: common physical-input router with press/release ownership, repeat filtering, captured gestures and an active-binding snapshot. Configuration changes never rewrite an in-flight suppression decision.
- `MouseHook.cs`: `WH_MOUSE_LL`, middle down/up only, injected-event filtering, retained callback and shutdown unhook. Movement, wheel, left/right and side buttons pass through.
- `KeyboardHook.cs`: delegates down/up routing to the same router; keeps its injected-input filtering.
- `RecordingGate.cs`: synchronous start/stop admission and generation checks before queued audio work.
- `TrayViewModel.cs`: connects input to the existing recording, processing, note and paste paths. Each session gets its own Recorder so late callbacks cannot dispose a subsequent session. A stop before the queued microphone start discards the empty capture. Esc invalidates the session immediately, then cleans up the session's own resources.
- `Recorder.cs`: cancellation completes a pending stop waiter, even if disposal prevents a native stopped callback.
- `Paster.cs`: adds physical modifier queries. Custom toggle gestures wait briefly for modifier release and fall back to the clipboard; no forced modifier key-up is injected.
- `SettingsWindow.axaml` / `.cs`: German toggle shortcut controls, capture/save/cancel, conflict messages and unchanged existing settings/secrets handling.
- `tests/Blitztext.Tests`: production router/gate/settings code tested with deterministic fake audio and result-delivery boundaries. Native integration and visual/UI interaction still require manual acceptance.

Native references: [LowLevelMouseProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc), [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc).
Callbacks only admit/route events; microphone, network and UI updates are dispatched afterwards. Windows can silently remove a slow low-level hook, so no blocking work is added to either callback.

## Compatibility and limitations

Old settings acquire the middle toggle default without changing their stored keyboard keys, workflows or preferences.
Invalid/overlapping additional bindings loaded from hand-edited JSON are disabled in memory; the settings window explains conflicts on save.
The existing API, model choices, DPAPI storage, note library, app identity and AppData location are unchanged.
The executable is an unsigned test build. Real input, NAudio, target focus, elevated targets, clipboard behavior and company endpoint rules are not established by the automated tests.
The Cloud environment publication issue does not affect this branch or GitHub Actions; builds/tests run on GitHub's Windows runner without installing an SDK on the user's PC.
