using System;
using System.Collections.Generic;
using System.Linq;

namespace Blitztext;

[Flags]
public enum ShortcutModifiers { None = 0, Control = 1, Alt = 2, Shift = 4, Windows = 8 }

public sealed class DictationShortcut
{
    public const int MiddleButton = 0x04;
    public bool Enabled { get; set; } = true;
    public int Key { get; set; } = MiddleButton;
    public ShortcutModifiers Modifiers { get; set; }
    public WorkflowKind Workflow { get; set; } = WorkflowKind.Transcribe;

    public DictationShortcut Copy() => new() { Enabled = Enabled, Key = Key, Modifiers = Modifiers, Workflow = Workflow };
    public static ShortcutModifiers ModifierFor(int key) => key switch
    {
        0x10 or 0xA0 or 0xA1 => ShortcutModifiers.Shift,
        0x11 or 0xA2 or 0xA3 => ShortcutModifiers.Control,
        0x12 or 0xA4 or 0xA5 => ShortcutModifiers.Alt,
        0x5B or 0x5C => ShortcutModifiers.Windows,
        _ => ShortcutModifiers.None
    };

    public static bool IsAssignableKey(int key) => key == MiddleButton ||
        (key >= 0x08 && key <= 0xFE && key != KeyNames.VK_ESCAPE &&
         ModifierFor(key) == ShortcutModifiers.None && key != 0xE7);

    public string? Validate(IEnumerable<int> legacyKeys)
    {
        if (!Enabled) return null;
        if (!IsAssignableKey(Key) || ((int)Modifiers & ~15) != 0 || !Enum.IsDefined(Workflow))
            return "Bitte einen gültigen Aufnahme-Shortcut wählen (Esc ist für Abbruch reserviert).";
        // Legacy hold keys match regardless of modifiers, including standalone right Ctrl/Shift.
        if (legacyKeys.Any(k => k == Key || (ModifierFor(k) & Modifiers) != 0))
            return "Der Aufnahme-Shortcut überschneidet sich mit einer bisherigen Hotkey-Taste.";
        if ((Modifiers.HasFlag(ShortcutModifiers.Windows) && Key is 0x4C or 0x55) ||
            (Modifiers.HasFlag(ShortcutModifiers.Alt) && Key is 0x09 or 0x73) ||
            (Modifiers.HasFlag(ShortcutModifiers.Control) && Modifiers.HasFlag(ShortcutModifiers.Alt) && Key == 0x2E))
            return "Diese Kombination ist für Windows reserviert. Bitte eine andere wählen.";
        return null;
    }

    public string DisplayName
    {
        get
        {
            var names = new List<string>();
            if (Modifiers.HasFlag(ShortcutModifiers.Control)) names.Add("Strg");
            if (Modifiers.HasFlag(ShortcutModifiers.Alt)) names.Add("Alt");
            if (Modifiers.HasFlag(ShortcutModifiers.Shift)) names.Add("Umschalt");
            if (Modifiers.HasFlag(ShortcutModifiers.Windows)) names.Add("Win");
            names.Add(Key == MiddleButton ? "Mittlere Maustaste" : KeyNames.FromVk(Key) ?? Key switch
            {
                >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)Key).ToString(),
                0x20 => "Leertaste", 0x09 => "Tab", 0x0D => "Enter", 0x08 => "Rücktaste",
                0x25 => "Links", 0x26 => "Oben", 0x27 => "Rechts", 0x28 => "Unten", 0x2E => "Entf",
                _ => $"Taste 0x{Key:X2}"
            });
            return string.Join(" + ", names);
        }
    }
}
