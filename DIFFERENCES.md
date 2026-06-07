# Differences from the original

<p><a href="DIFFERENCES.md"><b>🇬🇧 English</b></a> · <a href="UNTERSCHIEDE.md">🇩🇪 Deutsch</a></p>

Blitznotiz is a **standalone Windows rebuild** of
[Blitztext](https://github.com/cmagnussen/blitztext-app) (macOS) by Christoph Magnussen.
**No source code was carried over** — everything was built from scratch. This file records what stayed
the same and what changed.

## Platform & technology

|                     | Original "Blitztext"      | This rebuild "Blitznotiz"                       |
|---------------------|---------------------------|-------------------------------------------------|
| Operating system    | macOS                     | Windows                                         |
| Technology          | Swift / SwiftUI, Xcode    | .NET 9 / Avalonia 11                            |
| Distribution        | local Xcode build         | a single self-contained `.exe`, no .NET install |
| Secret storage      | macOS Keychain            | Windows DPAPI                                    |
| Code base           | —                         | complete reimplementation, not a shared line    |

## Kept (the original's idea)

- **Push-to-talk dictation:** hold a hotkey, speak, release, paste at the cursor.
- **The four dictation styles:** verbatim, improved, "vent" (calmer version), emoji.
- **Bring-your-own-key:** transcription via Whisper, rewriting via GPT.

## New in this build

- **⭐ Blitznotiz** — the main addition: voice notes that file themselves into a daily Markdown store,
  plus a dockable **library** to find and reuse them. See the
  [README](README.md#-blitznotiz--the-new-feature).
- **Server mode:** an optional OpenAI-compatible proxy that checks an app password and keeps the real
  key on the server — so others can use it without a key of their own. Restricted to `https://` and
  private/Tailscale addresses.
- **Clipboard auto-clear:** the pasted text is removed from the clipboard after a configurable number
  of seconds. The original deliberately leaves it on the clipboard.
- **Connection pre-warming:** the network connection (DNS + TCP + TLS) is opened ahead of time and the
  pooled connection is kept warm between dictations, so a dictation after a pause responds faster. (The
  original's "prewarm" only loads the local Whisper model.)
- **Own icon & branding**, independent of the original.

## Not (yet) carried over

- **Local transcription** (WhisperKit/CoreML) from the original is not implemented here yet; Blitznotiz
  currently uses the cloud / server path.

---

*With thanks to the original and its explicit invitation to build upon it.*
