# Mittelklick-Testversion: Start und Prüfung

Diese Fork-Version ergänzt einen Aufnahme-Shortcut: **Mittlere Maustaste drücken → sprechen → erneut drücken**.
Loslassen beendet die Aufnahme nicht. Standardmodus ist **Wörtlich**; die vorhandene Verarbeitung und das automatische Einfügen werden weiterverwendet.
Die bisherigen Halte-Hotkeys und die Blitznotiz-Bibliothek bleiben erhalten.

## Herunterladen und starten

1. Unter GitHub → Actions einen erfolgreichen `Windows build` für `codex/middle-mouse-toggle` öffnen.
2. Das Artefakt `Blitznotiz-win-x64-<Commit>` herunterladen und das ZIP vollständig entpacken.
3. Die ursprüngliche App über ihr Tray-Menü beenden. Original und Fork dürfen nicht gleichzeitig laufen.
4. Den Ordner `%APPDATA%\Blitztext` lokal sichern. Beide Versionen verwenden denselben Einstellungsordner und dieselben verschlüsselten Zugangsdaten.
5. `Blitztext.exe` aus dem entpackten Ordner starten. Ein .NET SDK oder eine separate .NET-Laufzeit ist nicht nötig.
6. `BUILD-INFO.txt` nennt den exakten Commit; `SHA256SUMS.txt` enthält die Prüfsumme der EXE.

Die Testversion ist nicht signiert. Auf einem verwalteten Arbeits-PC kann ihre Ausführung durch die Unternehmensrichtlinien gesperrt sein; solche Sperren nicht umgehen.
Der API-Zugang wird wie bisher in der App verwendet. Für den Build und die automatisierten Tests werden keine echten Schlüssel, Aufnahmen oder Benutzereinstellungen benötigt.

## Einstellungen

Unter **Workflows → Aufnahme-Shortcut (Start / Stopp)**:

- Zusatz-Shortcut aktivieren/deaktivieren.
- Über **Shortcut aufnehmen…** eine Tastenkombination oder den Mittelklick wählen. Alle Tasten loslassen, dann speichern. Esc bricht die Auswahl ab.
- **Mittlere Maustaste** stellt die Standardbelegung wieder her.
- Wörtlich, Verbessert, Dampf ablassen oder Emoji auswählen.

Der aktiv belegte Mittelklick wird in anderen Programmen unterdrückt, einschließlich Browser-Autoscroll und Tab-Aktionen. Das Drehen des Mausrads bleibt unverändert.
Bei deaktiviertem Zusatz-Shortcut funktioniert der Mittelklick wieder normal.
Die eingestellte Kombination muss mit denselben Zusatztasten gedrückt werden. Zusatztasten vor dem automatischen Einfügen loslassen; andernfalls bleibt der Text nach kurzer Wartezeit nur in der Zwischenablage.
Änderungen an der Belegung gelten ab der nächsten Aufnahme. Die laufende Aufnahme wird weiterhin mit ihrem ursprünglichen Shortcut beendet.
Das Aufnehmen einer neuen Belegung bricht eine laufende Aufnahme/Verarbeitung ab, damit die Auswahl keine Diktate auslöst.
Speichern übernimmt die Auswahl; Abbrechen/Schließen verwirft sie.

## Praktischer Windows-Test — noch manuell durchzuführen

1. In Editor, Browser-Textfeld und Word jeweils das Zieltextfeld fokussieren. Mittelklicken, einen Satz sprechen, mittelklicken: genau ein Ergebnis soll eingefügt werden.
2. Prüfen, dass Loslassen weiter aufnimmt, kein Autoscroll startet und normales Rad-Scrollen funktioniert.
3. Schnelle Klicks und sehr kurze Aufnahmen testen: kein doppeltes Einfügen, kein Hängenbleiben.
4. Esc während Aufnahme und Verarbeitung drücken: kein verspätetes Einfügen. Danach erneut diktieren.
5. Die bisherigen F9/F6/F8/F7-Haltefunktionen sowie F11-Notiz und F12-Bibliothek prüfen (bzw. die eigenen bisherigen Belegungen).
6. Zusatz-Shortcut deaktivieren, speichern und normalen Mittelklick prüfen. Danach wieder aktivieren.
7. Beispielsweise Strg+Umschalt+D aufnehmen; Start/Stopp, Zusatztasten loslassen, Speichern/Abbrechen und einen absichtlichen Konflikt mit F9 prüfen.
8. Während einer Aufnahme die Belegung ändern; die Aufnahme mit der bisherigen Belegung beenden, anschließend die neue testen.
9. Fensterwechsel, strenges Zielfenster, deaktiviertes Auto-Einfügen, Clipboard-Löschzeit und Autostart wie bisher prüfen.
10. App während einer Aufnahme beenden und neu starten. Keine hängende Aufnahme oder blockierte Taste.

Windows-reservierte Kombinationen, andere globale Hotkey-Programme und Maus-Treiber können Belegungen abfangen. Synthetische Eingaben von Remapping-Software werden absichtlich ignoriert. Für den ersten Test die physische mittlere Maustaste verwenden.
Einfügen in Programme mit höheren Windows-Rechten kann wie bisher scheitern. Ein erfolgreicher Build ist kein Beweis für reale Maus-, Mikrofon-, Zwischenablage- oder Zielprogramm-Funktion.

## Rückkehr zur ursprünglichen App

Fork beenden, Original starten. Für den exakten vorherigen Einstellungsstand bei beendeten Apps die lokale Sicherung wiederherstellen.
Die zusätzliche JSON-Einstellung wird von der unveränderten Originalversion ignoriert.

## Build und automatisierte Prüfungen

GitHub Actions verwendet .NET 9 auf Windows und erzeugt eine eigenständig lauffähige x64-Version.
Tests unter `tests/Blitztext.Tests` prüfen die produktive Eingabeverarbeitung, Sitzungszustände und Einstellungs-Kompatibilität. Audio, Verarbeitung und Auslieferung werden dabei simuliert; kein OpenAI-Aufruf und kein Zugriff auf AppData.
Der Teststand lässt sich im jeweiligen Workflow und im TRX-Testartefakt nachvollziehen.

Auf einem Windows-Entwicklungsrechner oder in CI:

```powershell
dotnet test tests/Blitztext.Tests/Blitztext.Tests.csproj -c Release --logger trx --results-directory TestResults
dotnet restore Blitztext.csproj -r win-x64 -p:SelfContained=true -p:PublishSingleFile=true
dotnet build Blitztext.csproj -c Release -r win-x64 --self-contained true --no-restore
dotnet publish Blitztext.csproj -c Release -r win-x64 --self-contained true --no-restore -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o dist
```

Unter Linux benötigt der App-Build zusätzlich `-p:EnableWindowsTargeting=true`. Die reine Testsuite ist plattformunabhängig.
Die bestehenden Avalonia-Abhängigkeiten bleiben bei `11.*`; es wurden keine Framework-, Modell- oder Provider-Upgrades vorgenommen.
Artefakte bleiben 14 Tage verfügbar; es wird kein öffentlicher Release erzeugt. Die MIT-Lizenz liegt als `LICENSE.txt` bei.
