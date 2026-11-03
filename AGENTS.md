# Blitznotiz fork: scope and verification

This is NitramTibas/Blitznotiz, forked from danielstefanmueller/Blitznotiz.
Upstream baseline: 2d19dd5d1ca2afd1aa9baac48d18d4e7b380c735.

## Authorized work

Prepare and implement a user-configurable additional dictation shortcut, with middle mouse button click-to-start / click-again-to-stop as the required default.
Preserve all existing keyboard defaults and hold-to-talk behavior, four dictation workflows, note storage/library, Esc cancellation, API settings, DPAPI secrets, clipboard behavior, original-window checks, tray behavior and autostart.
Use the existing speech-processing and delivery pipeline. No new providers, model changes, UI redesign, app renaming or unrelated refactoring.

Work on codex/middle-mouse-toggle in THIS fork. Do not push upstream or merge into main.
Do not create a public Release. GitHub Actions build artifacts are authorized.
Do not install development tools on the user's work PC. Build tools belong in Codex Cloud / GitHub Actions.
Never read, commit, upload or require the user's API keys, real dictation recordings, AppData settings or DPAPI files for builds/tests.

Read docs/CODEX_CLOUD_TASK.md for requirements and docs/BUILD_AND_TEST.md for checks.

## Build

Target remains net9.0-windows. Use the .NET 9 SDK.
Windows CI is .github/workflows/windows-build.yml; actions are pinned to verified commit hashes.
Preserve the MIT license.
If adding tests under tests/, exclude their source/resources from the root app project's recursive SDK globs.
Run meaningful state-transition and settings-compatibility tests. A build without tests is not an acceptance pass.
On Linux, use EnableWindowsTargeting=true where needed; Windows hooks/audio/DPAPI cannot be validated by a Linux-only test.

## Delivery

Report changed files, exact pushed commit, build result and artifact link, automated test results, and manual tests still pending.
A successful build is not proof of working middle-click, microphone input, clipboard delivery or compatibility with company application policy.
Do not claim the feature is implemented merely because this preparation commit or its baseline build succeeds.
