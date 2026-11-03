# Codex Cloud implementation task

## Outcome

Add an additional freely configurable dictation shortcut. The required ready-to-use default is the middle mouse button in toggle mode:
first click starts recording, release keeps recording, second click stops and processes once, then the existing auto-paste path inserts the text.

Preserve every existing feature and existing configured keyboard shortcut. Use an additional binding so F9/F6/F8/F7/F11/F12 retain their current behavior.
Default the new shortcut's dictation workflow to Transcribe (Wörtlich), and let the user select an existing dictation workflow. Do not route this default to Note: Note intentionally does not auto-paste.
Allow the additional shortcut to be disabled. Include a clear German explanation that its assigned mouse click is consumed while enabled.
Offer custom keyboard combinations as an alternative through a capture control; middle mouse support is the mandatory acceptance case. Do not hijack ordinary left/right mouse clicks.

## Repository and scope

- Work only in NitramTibas/Blitznotiz, branch codex/middle-mouse-toggle.
- main and danielstefanmueller/Blitznotiz remain untouched.
- The preparation commit only adds CI and documentation; it does not implement the feature.
- The Windows build workflow is already supplied. Fix build issues only as needed without unrelated behavior changes.
- Commit and push the implementation to the feature branch and check its GitHub Actions result. No upstream PR, merge or public Release.
- No real API key is needed for build or input-logic tests. Use fakes/mocks for recorder and network boundaries.

## Code map

- KeyboardHook.cs: WH_KEYBOARD_LL, key press/release events, injected-event filtering, SwallowKey.
- KeyNames.cs: limited single-key map and preset list.
- Settings.cs: JSON settings with legacy HotkeyConfig strings. Add backward-compatible optional shortcut settings.
- SettingsWindow.axaml / .cs: German settings controls, save/cancel, duplicate validation.
- TrayViewModel.cs: RebuildHotkeyMap, OnKeyPressed, OnKeyReleased, StartCapture, HandleEscape, StopAndProcessAsync, Dispose.
- Recorder.cs / OpenAiClient.cs / Workflows.cs / Paster.cs / BlitzNotes.cs: preserve existing behavior and reuse their pipeline.
- Add a small MouseHook.cs and a testable input/state component if appropriate.

## Implementation requirements

1. Use WH_MOUSE_LL for real global mouse events. Middle-button DOWN/UP are separate from wheel scrolling. Keep native callbacks fast, retain delegates, report installation errors, call CallNextHookEx for unrelated events, and unhook on shutdown.
2. Toggle only once per physical press. Track pressed state / release; keyboard auto-repeat must not toggle repeatedly.
3. Suppression must balance down/up. Remember consumed presses until release even if settings, modifiers or state change; do not leave target apps with unmatched events. Swallow the assigned middle-click to prevent autoscroll/tab actions, but pass unrelated input through.
4. Ignore injected events appropriately, including self-generated paste input. Test/document interaction with mouse remapping software.
5. Do not start another recording while processing. A keyboard hold recording must not be stopped by an unrelated mouse shortcut. Preserve the active trigger and workflow for each recording.
6. Capture the paste target at start. Reuse existing strict-target and clipboard settings; avoid changing paste semantics. Ensure held modifier keys do not corrupt Ctrl+V delivery for custom keyboard shortcuts.
7. Esc cancels recording/processing and invalidates stale work. Start/stop races, microphone failures, rapid clicks, reloads and shutdown must not strand recording or deliver twice.
8. During shortcut capture, normal shortcut actions are suspended. Consume the complete capture gesture before re-enabling actions. Esc cancels capture; closing/canceling settings restores the prior configuration.
9. Validate duplicates/overlapping bindings, normalize combinations, preserve legacy settings and secrets, and apply saved bindings without restarting.
10. Keep the current tray recording/processing indications. Update only shortcut-related UI/help text.

## Automated acceptance tests

Use deterministic tests with fake recorder, processor and delivery; no physical input or OpenAI calls:
- first middle down starts once; middle up does not stop; next down stops/processes once;
- duplicate down / keyboard repeat, rapid down-up-down, and very short recordings;
- disabled shortcut and unrelated mouse/wheel input pass through;
- balanced suppression across modifiers, capture mode, reload, cancel and processing;
- all existing hold shortcuts still stop only on their own release;
- Esc and late asynchronous completions do not paste canceled results;
- microphone/start/processing failure resets state;
- old settings load with original hotkeys and preferences intact;
- capture/save/cancel and conflict validation preserve prior values.

Create test projects under tests/ and wire them into the supplied CI. Prevent root app compilation from including test sources.

## Manual Windows acceptance (must not be marked passed without evidence)

- Start original-version and fork comparisons separately, with a backup of %APPDATA%/Blitztext.
- In an editor, browser text field and Word: focus field, middle-click, speak, middle-click; one result appears.
- Verify release does not stop recording; no browser autoscroll/tab action; wheel scroll remains normal.
- Verify F9/F6/F8/F7, F11 note storage, F12 library, Esc, clipboard clearing, target switching and autostart.
- Verify saving/closing settings during relevant states, repeated sessions and shutdown.
- Check elevated target apps separately; document existing Windows input-permission limits.
- No claim of physical mouse/microphone acceptance from Linux or headless CI alone.

## Report

Return implementation commit, concise diff, tests actually run, Windows CI URL, downloadable artifact URL, and remaining manual checks. Make a clear distinction between prepared, implemented, built, and manually verified.
