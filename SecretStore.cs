using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Blitztext;

/// <summary>
/// Speichert Geheimnisse (OpenAI-Schlüssel, App-Passwort) verschlüsselt via Windows DPAPI
/// (an das Windows-Benutzerkonto gebunden). Klartext liegt nie in der settings.json.
/// </summary>
public static class SecretStore
{
    public const string OpenAiKey = "openai_key";
    public const string AppPassword = "app_password";

    private static string PathFor(string name) => Path.Combine(Settings.Dir, name + ".dat");

    public static void Save(string name, string? value)
    {
        Directory.CreateDirectory(Settings.Dir);
        var path = PathFor(name);
        if (string.IsNullOrEmpty(value))
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(value), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
        File.WriteAllBytes(path, encrypted);
    }

    public static string Load(string name)
    {
        var path = PathFor(name);
        if (!File.Exists(path)) return "";
        try
        {
            var plain = ProtectedData.Unprotect(
                File.ReadAllBytes(path), optionalEntropy: null, scope: DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return "";
        }
    }
}
