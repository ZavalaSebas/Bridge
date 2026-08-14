using System.Runtime.InteropServices;

namespace Bridge.Services;

/// <summary>
/// One process from a snapshot: its PID and its parent's PID. The parent link
/// is what lets Bridge track launchers that spawn the real game and then exit
/// (Genshin, Epic/GOG frontends, emulator frontends) — see TrackProcessTree.
/// </summary>
public readonly record struct ProcessEntry(int Pid, int ParentPid);

/// <summary>
/// Enumerates every running process with its parent PID via the Win32
/// Toolhelp32 snapshot API — one native call returns all processes with their
/// parent ids, so a poll is a single snapshot instead of one WMI/Process query
/// per process. Pure logic (the tree expansion) lives in
/// <see cref="ProcessTreeExpander"/> and is unit-tested separately; this class
/// is the OS boundary, verified by launching the app.
/// </summary>
public static class ProcessTreeSnapshot
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;

    public static List<ProcessEntry> Collect()
    {
        var result = new List<ProcessEntry>();
        var snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == IntPtr.Zero)
        {
            return result;
        }

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (!Process32First(snapshot, ref entry))
            {
                return result;
            }

            do
            {
                result.Add(new ProcessEntry((int)entry.th32ProcessID, (int)entry.th32ParentProcessID));
            } while (Process32Next(snapshot, ref entry));
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return result;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
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

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32First(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool Process32Next(IntPtr hSnapshot, ref PROCESSENTRY32 lppe);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr hObject);
}
