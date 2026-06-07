using System;
using Microsoft.Win32;

namespace Blitztext;

/// <summary>
/// Autostart über den Windows-"Run"-Schlüssel des aktuellen Benutzers. Kein Admin-Recht nötig.
/// </summary>
public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = AppBrand.AutostartValueName;

    private static string ExePath => Environment.ProcessPath ?? "";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (key == null) return;

            if (enabled)
            {
                if (!string.IsNullOrEmpty(ExePath))
                    key.SetValue(ValueName, $"\"{ExePath}\"");
            }
            else if (key.GetValue(ValueName) != null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Autostart ist optional – Fehler hier dürfen die App nicht stören.
        }
    }
}
