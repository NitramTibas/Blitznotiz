# Unterschiede zum Original

<p><a href="DIFFERENCES.md">🇬🇧 English</a> · <a href="UNTERSCHIEDE.md"><b>🇩🇪 Deutsch</b></a></p>

Blitznotiz ist ein **eigenständiger Windows-Neubau** von
[Blitztext](https://github.com/cmagnussen/blitztext-app) (macOS) von Christoph Magnussen.
**Kein Quellcode wurde übernommen** — alles wurde von Grund auf neu gebaut. Diese Datei hält fest, was
gleich blieb und was sich geändert hat.

## Plattform & Technik

|                  | Original „Blitztext"      | Dieser Eigenbau „Blitznotiz"                       |
|------------------|---------------------------|----------------------------------------------------|
| Betriebssystem   | macOS                     | Windows                                            |
| Technik          | Swift / SwiftUI, Xcode    | .NET 9 / Avalonia 11                               |
| Verteilung       | lokaler Xcode-Build       | eine eigenständige `.exe`, kein .NET-Install nötig |
| Geheimnis-Speicher | macOS-Schlüsselbund     | Windows DPAPI                                       |
| Code-Basis       | —                         | komplette Neu-Implementierung, keine geteilte Zeile |

## Gleich geblieben (die Idee des Originals)

- **Push-to-talk-Diktat:** Hotkey halten, sprechen, loslassen, Text am Cursor einfügen.
- **Die vier Diktat-Stile:** wörtlich, verbessert, „Dampf ablassen" (ruhigere Fassung), Emoji.
- **Bring-your-own-Key:** Transkription über Whisper, Umschreiben über GPT.

## Neu in diesem Eigenbau

- **⭐ Blitznotiz** — die Haupt-Erweiterung: Sprach-Notizen, die sich automatisch in eine Tages-Markdown-
  Ablage einsortieren, plus eine andockbare **Bibliothek** zum Wiederfinden und Wiederverwenden. Siehe
  [README](README.de.md#-blitznotiz--die-neue-erweiterung).
- **Server-Modus:** ein optionaler OpenAI-kompatibler Proxy, der ein App-Passwort prüft und den echten
  Schlüssel auf dem Server hält — so können andere ohne eigenen Schlüssel mitnutzen. Beschränkt auf
  `https://` und private/Tailscale-Adressen.
- **Eigenes Icon & Branding**, unabhängig vom Original.

## (Noch) nicht übernommen

- **Lokale Transkription** (WhisperKit/CoreML) aus dem Original ist hier noch nicht umgesetzt; Blitznotiz
  nutzt aktuell den Cloud- bzw. Server-Weg.

---

*Mit Dank an das Original und seine ausdrückliche Einladung, darauf aufzubauen.*
