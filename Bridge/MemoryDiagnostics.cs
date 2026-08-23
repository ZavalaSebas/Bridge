// TEMP: memory diagnostics instrumentation (diagnóstico de RAM en reposo).
// Reversible: borra este archivo, quita las llamadas `Bridge.MemoryDiagnostics.*`
// (todas // TEMP) y el accesor `RemoteImageCache.MemorySnapshot()`.
// Solo mide, no cambia comportamiento. Escribe a
// %LOCALAPPDATA%\Bridge\logs\memory-diagnostics.log y a Debug.
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Bridge.Converters;
using Bridge.ViewModels;

namespace Bridge;

internal static class MemoryDiagnostics
{
    private static readonly object Lock = new();
    private static Timer? _periodic;

    private static string LogPath => Path.Combine(Config.AppDataPath, "logs", "memory-diagnostics.log");

    // Captures a labeled breakdown of process + managed memory plus the app's
    // biggest in-memory caches/collections. When settle is true a full GC runs
    // first, so the managed number reflects genuinely retained memory (what would
    // survive a collection) instead of not-yet-collected garbage — use sparingly.
    public static void Snapshot(string label, MainViewModel? vm = null, bool settle = false)
    {
        try
        {
            if (settle)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            using var proc = Process.GetCurrentProcess();

            var sb = new StringBuilder();
            sb.AppendLine($"=== {label,-32} {DateTime.Now:HH:mm:ss} (settle={settle}) ===");
            sb.AppendLine($"  WorkingSet     {Mb(proc.WorkingSet64),8} MB   (what Task Manager shows)");
            sb.AppendLine($"  PrivateBytes   {Mb(proc.PrivateMemorySize64),8} MB");
            sb.AppendLine($"  ManagedHeap    {Mb(GC.GetTotalMemory(false)),8} MB   Gen0={GC.CollectionCount(0)} Gen1={GC.CollectionCount(1)} Gen2={GC.CollectionCount(2)}");

            var (imgCount, imgBytes) = RemoteImageCache.MemorySnapshot();
            sb.AppendLine($"  ImageCache     {Mb(imgBytes),8} MB   ({imgCount} decoded bitmaps)");

            if (vm is not null)
                sb.AppendLine($"  Collections            Games={vm.Games.Count}  DetailedRows={vm.DetailedRows.Count}");

            // WebView2 helper processes only exist while a browser window (SteamGridDB
            // sign-in) is open — count them to confirm they are gone at rest.
            var wv2 = Process.GetProcessesByName("msedgewebview2");
            long wv2Bytes = 0;
            foreach (var p in wv2)
            {
                try { wv2Bytes += p.WorkingSet64; } catch { /* exited */ }
                finally { p.Dispose(); }
            }
            sb.AppendLine($"  WebView2       {Mb(wv2Bytes),8} MB   ({wv2.Length} procs, expect 0 at rest)");

            Write(sb.ToString());
        }
        catch
        {
            // Nunca romper el arranque/idle por instrumentar.
        }
    }

    // Logs a snapshot every 30 s so idle growth (a slow leak) is visible in the
    // log without any user interaction. Non-forcing read (settle=false) to avoid
    // perturbing the measurement with periodic full GCs.
    public static void StartPeriodic(MainViewModel vm)
    {
        _periodic ??= new Timer(
            _ => Snapshot("idle tick", vm),
            null,
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(30));
    }

    private static string Mb(long bytes) => (bytes / (1024.0 * 1024.0)).ToString("F1");

    private static void Write(string text)
    {
        Debug.Write("[MemoryDiagnostics] " + text);
        lock (Lock)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, text);
            }
            catch
            {
                // Nunca romper por instrumentar.
            }
        }
    }
}
