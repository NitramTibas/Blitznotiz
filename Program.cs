using System;
using System.IO;
using System.Threading;
using Avalonia;

namespace Blitztext;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Versteckter Bau-Pfad: erzeugt die App-Icon-.ico aus der Vektor-Routine, ohne UI.
        // Blitztext.exe --gen-icon <pfad.ico>
        if (args.Length >= 2 && args[0] == "--gen-icon")
        {
            try
            {
                BuildAvaloniaApp().SetupWithoutStarting();
                BtIconGenerator.Generate(args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(args[1] + ".error.log", ex.ToString()); } catch { }
                return 2;
            }
        }

        if (args.Length >= 2 && args[0] == "--gen-preview")
        {
            try
            {
                BuildAvaloniaApp().SetupWithoutStarting();
                BtIconGenerator.GeneratePreviews(args[1]);
                return 0;
            }
            catch (Exception ex)
            {
                try { File.WriteAllText(Path.Combine(args[1], "preview.error.log"), ex.ToString()); } catch { }
                return 2;
            }
        }

        // Nur eine laufende Instanz zulassen (neuer, eigener Mutex – kein Codex).
        using var mutex = new Mutex(initiallyOwned: true, AppBrand.MutexName, out bool createdNew);
        if (!createdNew) return 0;

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            try { mutex.ReleaseMutex(); } catch (ApplicationException) { /* not owned at exit — okay */ }
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
