using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Blitztext;

/// <summary>
/// Leichtgewichtige asynchrone Diagnose. Log-Aufrufe kehren sofort zurück, damit Aufnahme,
/// Warm-up und Einfügen nicht auf Disk-I/O warten. Flush() leert die Queue synchron beim Beenden
/// (verhindert den Verlust der letzten Zeilen – Review-Befund).
/// </summary>
public static class Logger
{
    private static readonly ConcurrentQueue<string> Queue = new();
    private static readonly SemaphoreSlim Signal = new(0);
    private static readonly object FlushLock = new();
    private static int _started;

    private static string FilePath => Path.Combine(Settings.Dir, "debug.log");

    public static void Log(string message)
    {
        Queue.Enqueue($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
        Signal.Release();
        EnsureWorker();
    }

    /// <summary>Synchron alle ausstehenden Zeilen schreiben (z.B. beim App-Beenden).</summary>
    public static void Flush()
    {
        lock (FlushLock)
        {
            try
            {
                if (Queue.IsEmpty) return;
                Directory.CreateDirectory(Settings.Dir);
                using var writer = new StreamWriter(FilePath, append: true);
                while (Queue.TryDequeue(out var line)) writer.WriteLine(line);
                writer.Flush();
            }
            catch { while (Queue.TryDequeue(out _)) { } }
        }
    }

    private static void EnsureWorker()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        _ = Task.Run(FlushLoopAsync);
    }

    private static async Task FlushLoopAsync()
    {
        while (true)
        {
            await Signal.WaitAsync().ConfigureAwait(false);
            lock (FlushLock)
            {
                try
                {
                    Directory.CreateDirectory(Settings.Dir);
                    using var writer = new StreamWriter(FilePath, append: true);
                    do
                    {
                        while (Queue.TryDequeue(out var line))
                            writer.WriteLine(line);
                        writer.Flush();
                    }
                    while (Signal.Wait(0));
                }
                catch
                {
                    while (Queue.TryDequeue(out _)) { }
                }
            }
        }
    }
}
