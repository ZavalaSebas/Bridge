// TEMP: memory diagnostics instrumentation (diagnóstico de RAM en reposo).
// Reversible: borra este archivo, quita las llamadas `Bridge.MemoryDiagnostics.*`
// (todas // TEMP) y el accesor `RemoteImageCache.MemorySnapshot()`.
// Solo mide, no cambia comportamiento. Escribe a
// %LOCALAPPDATA%\Bridge\logs\memory-diagnostics.log y a Debug.
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
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

            // WebView2 helper processes are global in the OS. Report both:
            // - Global total (all msedgewebview2.exe on the machine)
            // - Bridge-owned total (descendants of this Bridge process)
            var wv2 = Process.GetProcessesByName("msedgewebview2");
            long wv2GlobalBytes = 0;
            long wv2OwnedBytes = 0;
            var bridgeDescendants = GetDescendantProcessIds(proc.Id);
            var wv2OwnedCount = 0;
            foreach (var p in wv2)
            {
                try
                {
                    var ws = p.WorkingSet64;
                    wv2GlobalBytes += ws;
                    if (bridgeDescendants.Contains(p.Id))
                    {
                        wv2OwnedBytes += ws;
                        wv2OwnedCount++;
                    }
                }
                catch
                {
                    // Exited during sampling.
                }
                finally { p.Dispose(); }
            }
            sb.AppendLine($"  WebView2Global {Mb(wv2GlobalBytes),8} MB   ({wv2.Length} procs, machine-wide)");
            sb.AppendLine($"  WebView2Bridge {Mb(wv2OwnedBytes),8} MB   ({wv2OwnedCount} procs, owned by Bridge)");

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

    private static HashSet<int> GetDescendantProcessIds(int rootPid)
    {
        var childrenByParent = BuildChildrenIndex();
        var descendants = new HashSet<int> { rootPid };
        var queue = new Queue<int>();
        queue.Enqueue(rootPid);

        while (queue.Count > 0)
        {
            var parentPid = queue.Dequeue();
            if (!childrenByParent.TryGetValue(parentPid, out var children))
                continue;

            foreach (var childPid in children)
            {
                if (!descendants.Add(childPid))
                    continue;

                queue.Enqueue(childPid);
            }
        }

        return descendants;
    }

    private static Dictionary<int, List<int>> BuildChildrenIndex()
    {
        var childrenByParent = new Dictionary<int, List<int>>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandleValue)
            return childrenByParent;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry))
                return childrenByParent;

            do
            {
                var parentPid = unchecked((int)entry.th32ParentProcessID);
                var pid = unchecked((int)entry.th32ProcessID);
                if (!childrenByParent.TryGetValue(parentPid, out var children))
                {
                    children = [];
                    childrenByParent[parentPid] = children;
                }

                children.Add(pid);
            }
            while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return childrenByParent;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandleValue = new(-1);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

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
