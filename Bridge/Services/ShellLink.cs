using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Bridge.Services;

/// <summary>
/// Resolves a Windows shortcut (.lnk) to its target executable without the WSH
/// scripting dependency Playnite uses (WshShell.CreateShortcut). Uses the native
/// IShellLink COM interface via a small p/invoke shim.
/// </summary>
internal static class ShellLink
{
    public static string? ResolveTarget(string lnkPath)
    {
        if (!File.Exists(lnkPath))
            return null;

        try
        {
            var shellLink = (IShellLinkW)new ShellLinkComObject();
            var persistFileIid = IPersistFileIid;
            var unknown = Marshal.GetIUnknownForObject(shellLink);
            try
            {
                if (Marshal.QueryInterface(unknown, in persistFileIid, out var persistFile) != 0)
                {
                    return null;
                }

                try
                {
                    var file = (IPersistFile)Marshal.GetObjectForIUnknown(persistFile);
                    file.Load(lnkPath, (int)STGM_READ);

                    var path = new StringBuilder(260);
                    shellLink.GetPath(path, path.Capacity, IntPtr.Zero, SLGP_RAWPATH);
                    return path.Length > 0 ? path.ToString() : null;
                }
                finally
                {
                    Marshal.Release(persistFile);
                }
            }
            finally
            {
                Marshal.Release(unknown);
            }
        }
        catch
        {
            return null;
        }
    }

    private static readonly Guid IPersistFileIid = new("0000010b-0000-0000-c000-000000000046");
    private const int STGM_READ = 0;
    private const uint SLGP_RAWPATH = 0x4;

    [ComImport, Guid("00021401-0000-0000-c000-000000000046")]
    private class ShellLinkComObject
    {
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("000214f9-0000-0000-c000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, uint fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0000010b-0000-0000-c000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
