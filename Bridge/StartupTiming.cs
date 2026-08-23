// TEMP: startup timing instrumentation (diagnóstico de arranque).
// Reversible: borra este archivo y quita las líneas `Bridge.StartupTiming.` (todas // TEMP).
// Solo mide, no cambia comportamiento. Escribe a %LOCALAPPDATA%\Bridge\logs\startup-timing.log y a Debug.
using System.Diagnostics;
using System.IO;

namespace Bridge;

internal static class StartupTiming
{
    private static readonly Stopwatch Watch = new();
    private static readonly object Lock = new();
    private static long _lastMs;
    private static bool _active;

    private static string LogPath => Path.Combine(Config.AppDataPath, "logs", "startup-timing.log");

    public static void Begin()
    {
        lock (Lock)
        {
            Watch.Restart();
            _lastMs = 0;
            _active = true;
            Write($"=== startup {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
        }
    }

    public static void Mark(string label)
    {
        lock (Lock)
        {
            if (!_active) return;
            var now = Watch.ElapsedMilliseconds;
            Write($"{label,-44} +{now - _lastMs,6} ms   (t={now,6} ms)");
            _lastMs = now;
        }
    }

    public static void Note(string text)
    {
        lock (Lock)
        {
            if (_active) Write($"  · {text}");
        }
    }

    public static void Stop(string label)
    {
        lock (Lock)
        {
            if (!_active) return;
            var now = Watch.ElapsedMilliseconds;
            Write($"{label,-44} +{now - _lastMs,6} ms   (t={now,6} ms)");
            _active = false;
        }
    }

    private static void Write(string line)
    {
        Debug.WriteLine("[StartupTiming] " + line);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch
        {
            // Nunca romper el arranque por instrumentar.
        }
    }
}
