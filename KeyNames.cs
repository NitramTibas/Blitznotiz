using System;
using System.Collections.Generic;

namespace Blitztext;

/// <summary>
/// Übersetzt Tastennamen (wie in den Einstellungen gespeichert, z.B. "F9") in Windows
/// Virtual-Key-Codes und zurück – ohne WinForms-Abhängigkeit (ersetzt System.Windows.Forms.Keys).
/// Bewusst auf die für Push-to-talk sinnvollen, neutralen Tasten beschränkt.
/// </summary>
internal static class KeyNames
{
    public const int VK_ESCAPE = 0x1B;

    private static readonly Dictionary<string, int> NameToVk = Build();

    private static Dictionary<string, int> Build()
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // Funktionstasten F1..F24 = 0x70..0x87
        for (int i = 1; i <= 24; i++) map["F" + i] = 0x70 + (i - 1);
        map["Pause"] = 0x13;
        map["Insert"] = 0x2D;
        map["Home"] = 0x24;
        map["End"] = 0x23;
        map["PageUp"] = 0x21;
        map["PageDown"] = 0x22;
        map["Scroll"] = 0x91;
        map["Apps"] = 0x5D;        // Kontextmenü-Taste
        map["RControlKey"] = 0xA3;
        map["RShiftKey"] = 0xA1;
        return map;
    }

    public static int? ToVk(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return NameToVk.TryGetValue(name.Trim(), out var vk) ? vk : null;
    }

    public static string? FromVk(int vk)
    {
        foreach (var kv in NameToVk)
            if (kv.Value == vk) return kv.Key;
        return null;
    }

    /// <summary>Namen, die in einem Hotkey-Auswahlfeld angeboten werden.</summary>
    public static IReadOnlyList<string> Selectable { get; } = new[]
    {
        "F6", "F7", "F8", "F9", "F2", "F3", "F4", "F11", "F12", "Pause", "Insert", "Apps"
    };
}
