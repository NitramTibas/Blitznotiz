using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Blitztext;

/// <summary>
/// Eine einzelne Blitznotiz-Zeile aus einer Tages-Datei.
/// Identität (für Markieren/Löschen) = Datei-Datum + Zeilennummer; vor dem Schreiben wird zusätzlich
/// Zeit+Text gegen die Zeile geprüft, damit eine extern geänderte Datei nicht die falsche Notiz trifft.
/// </summary>
public sealed class NoteEntry
{
    public DateOnly Date { get; init; }     // welche Tages-Datei
    public int LineIndex { get; init; }     // 0-basierte Zeile in dieser Datei
    public string Time { get; init; } = ""; // "HH:MM"
    public string Text { get; init; } = "";
    public bool Done { get; init; }         // true = wiederverwendet (- [x])
}

/// <summary>
/// Ablage der Blitznotizen als lesbare Markdown-Tagesdateien:
/// <c>&lt;Wurzel&gt;\2026\2026-06\2026-06-04.md</c>, je Notiz eine Zeile
/// <c>- [ ] HH:MM Text</c> (nach Wiederverwendung <c>- [x] …</c>).
/// Reine Datei-Logik, kein UI. Die Datei ist die alleinige Wahrheit – nach jeder Änderung neu laden.
/// Geschrieben wird immer atomar (tmp + move) und mit einheitlichen Windows-Zeilenenden.
/// </summary>
public static class BlitzNoteStore
{
    // Toleriert führende Leerzeichen, [ ]/[x]/[X], variable Abstände, auch leeren Text.
    // Gruppe 1 = Status, 2 = Zeit, 3 = Text.
    private static readonly Regex LinePattern =
        new(@"^\s*-\s*\[(?<done>[ xX])\]\s*(?<time>\d{1,2}:\d{2})\s*(?<text>.*?)\s*$", RegexOptions.Compiled);

    /// <summary>Effektiver Wurzel-Ordner: gewählter Pfad oder Standard (Dokumente\Blitznotizen).</summary>
    public static string ResolveRoot(Settings settings)
    {
        var root = settings.BlitzNoteRoot;
        if (string.IsNullOrWhiteSpace(root))
            root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Blitznotizen");
        return root;
    }

    public static string DayFilePath(string root, DateOnly day) =>
        Path.Combine(root,
            day.Year.ToString("0000", CultureInfo.InvariantCulture),
            day.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ".md");

    private static string MonthDir(string root, int year, int month) =>
        Path.Combine(root,
            year.ToString("0000", CultureInfo.InvariantCulture),
            $"{year:0000}-{month:00}");

    // ── Schreiben ───────────────────────────────────────────────────────────────
    /// <summary>Hängt eine neue Notiz an die Tages-Datei an (legt Ordner/Datei bei Bedarf an).</summary>
    public static void Append(Settings settings, DateTime when, string text)
    {
        var root = ResolveRoot(settings);
        var day = DateOnly.FromDateTime(when);
        var path = DayFilePath(root, day);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var lines = File.Exists(path)
            ? File.ReadAllLines(path).ToList()
            : new List<string> { "# Blitznotizen " + day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), "" };

        lines.Add($"- [ ] {when.ToString("HH:mm", CultureInfo.InvariantCulture)} {OneLine(text)}");
        WriteAllLines(path, lines);
    }

    /// <summary>Eine Notiz ist genau eine Zeile – Umbrüche im gesprochenen Text glätten.</summary>
    private static string OneLine(string text) =>
        Regex.Replace(text.Trim(), @"\s*[\r\n]+\s*", " ");

    // ── Lesen ────────────────────────────────────────────────────────────────────
    public static List<NoteEntry> LoadDay(string root, DateOnly day)
    {
        var result = new List<NoteEntry>();
        var path = DayFilePath(root, day);
        if (!File.Exists(path)) return result;

        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (Exception ex) { Logger.Log($"Blitznotiz lesen fehlgeschlagen ({path}): {ex.Message}"); return result; }

        for (int i = 0; i < lines.Length; i++)
        {
            var m = LinePattern.Match(lines[i]);
            if (!m.Success) continue;
            result.Add(new NoteEntry
            {
                Date = day,
                LineIndex = i,
                Time = m.Groups["time"].Value,
                Text = m.Groups["text"].Value,
                Done = m.Groups["done"].Value is "x" or "X",
            });
        }
        return result;
    }

    public static List<NoteEntry> LoadMonth(string root, int year, int month)
    {
        var dir = MonthDir(root, year, month);
        var all = new List<NoteEntry>();
        if (!Directory.Exists(dir)) return all;

        foreach (var file in Directory.EnumerateFiles(dir, "*.md"))
        {
            var name = Path.GetFileNameWithoutExtension(file);
            if (!DateOnly.TryParseExact(name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
                continue;
            all.AddRange(LoadDay(root, day));
        }
        // Chronologisch: nach Tag, dann nach Uhrzeit, dann nach Zeilenposition (stabil bei gleicher Zeit).
        return all
            .OrderBy(n => n.Date)
            .ThenBy(n => n.Time, StringComparer.Ordinal)
            .ThenBy(n => n.LineIndex)
            .ToList();
    }

    // ── Ändern ───────────────────────────────────────────────────────────────────
    /// <summary>Setzt den Häkchen-Zustand einer einzelnen Notiz (Wiederverwenden ⇒ true).</summary>
    public static void SetDone(string root, NoteEntry entry, bool done)
    {
        var path = DayFilePath(root, entry.Date);
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        if (!IsSameNote(lines, entry)) return; // Datei extern geändert – nichts erzwingen
        lines[entry.LineIndex] = $"- [{(done ? "x" : " ")}] {entry.Time} {entry.Text}".TrimEnd();
        WriteAllLines(path, lines);
    }

    /// <summary>Löscht eine einzelne Notiz-Zeile (kein Rückgängig).</summary>
    public static void Delete(string root, NoteEntry entry)
    {
        var path = DayFilePath(root, entry.Date);
        if (!File.Exists(path)) return;
        var lines = File.ReadAllLines(path);
        if (!IsSameNote(lines, entry)) return;
        var list = lines.ToList();
        list.RemoveAt(entry.LineIndex);
        WriteAllLines(path, list);
    }

    /// <summary>Stellt sicher, dass an LineIndex noch genau diese Notiz steht (Zeit+Text), nicht nur irgendeine.</summary>
    private static bool IsSameNote(string[] lines, NoteEntry entry)
    {
        if (entry.LineIndex < 0 || entry.LineIndex >= lines.Length) return false;
        var m = LinePattern.Match(lines[entry.LineIndex]);
        return m.Success && m.Groups["time"].Value == entry.Time && m.Groups["text"].Value == entry.Text;
    }

    /// <summary>Entfernt Notiz-Zeilen eines Tages – nur erledigte oder alle. Nicht-Notiz-Zeilen bleiben.</summary>
    public static void DeleteDay(string root, DateOnly day, bool onlyUsed)
    {
        var path = DayFilePath(root, day);
        if (!File.Exists(path)) return;
        FilterFile(path, onlyUsed);
    }

    /// <summary>Wie <see cref="DeleteDay"/>, aber über alle Tages-Dateien des Monats.</summary>
    public static void DeleteMonth(string root, int year, int month, bool onlyUsed)
    {
        var dir = MonthDir(root, year, month);
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*.md").ToList())
            FilterFile(file, onlyUsed);
    }

    private static void FilterFile(string path, bool onlyUsed)
    {
        string[] lines;
        try { lines = File.ReadAllLines(path); }
        catch (Exception ex) { Logger.Log($"Blitznotiz aufräumen fehlgeschlagen ({path}): {ex.Message}"); return; }

        var kept = new List<string>();
        foreach (var line in lines)
        {
            var m = LinePattern.Match(line);
            if (!m.Success) { kept.Add(line); continue; }          // fremde/Kopf-Zeilen behalten
            bool isDone = m.Groups["done"].Value is "x" or "X";
            bool remove = onlyUsed ? isDone : true;
            if (!remove) kept.Add(line);
        }
        WriteAllLines(path, kept);
    }

    private static void WriteAllLines(string path, IReadOnlyList<string> lines)
    {
        // Atomar schreiben (tmp + move), damit ein Absturz die Datei nie halb hinterlässt.
        // Einheitliche Windows-Zeilenenden, damit erneutes Parsen verlässlich bleibt.
        var nl = Environment.NewLine;
        var content = lines.Count == 0 ? "" : string.Join(nl, lines) + nl;
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, overwrite: true);
    }
}
