using System;
using System.IO;
using System.Runtime.InteropServices;

// System.Diagnostics.Process.Start is unreliable from Unity's main thread on this
// Mono player: UseShellExecute=true throws a misleading Win32Exception("Success") via
// its COM/ShellExecuteEx path (Unity's main thread is never a COM STA apartment), and
// UseShellExecute=false has also been observed to throw the same "Native error= Success"
// when launching a child exe with arguments. CreateProcessW is the raw Win32 API
// underneath both — calling it directly avoids whichever .NET marshaling/COM layer is
// misbehaving.
//
// Duplicated verbatim in the launcher, the lobby and every game. Keep the copies identical.
public static class ProcessLauncher
{
    /// <summary>
    /// Starts <paramref name="exePath"/> with the given arguments. Never throws: a failure
    /// comes back as false plus a message, because every caller is mid-transition and needs
    /// to decide whether to stay open rather than unwind through an exception.
    /// </summary>
    public static bool TryStart(string exePath, string arguments, out string error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(exePath))
        {
            error = "No executable path was given.";
            return false;
        }

        try
        {
            // Unity boots by resolving <exe name>_Data relative to the current directory.
            // Without this the child inherits OUR working directory, cannot find its own
            // _Data folder, and silently exits right after starting.
            string workingDirectory = SafeDirectoryOf(exePath);

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            string commandLine = $"\"{exePath}\" {arguments}";

            var startupInfo = new STARTUPINFO();
            startupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFO));

            bool ok = CreateProcessW(
                null, commandLine, IntPtr.Zero, IntPtr.Zero, false,
                0, IntPtr.Zero, workingDirectory, ref startupInfo, out PROCESS_INFORMATION processInfo);

            if (!ok)
            {
                error = $"CreateProcessW failed with Win32 error {Marshal.GetLastWin32Error()}.";
                return false;
            }

            // Fire and forget: the child owns its own lifetime, and holding these handles
            // open would keep a zombie entry alive after it exits.
            CloseHandle(processInfo.hProcess);
            CloseHandle(processInfo.hThread);
            return true;
#else
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath)
            {
                Arguments = arguments,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory
            });
            return true;
#endif
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Path.GetDirectoryName throws on a malformed path (and a mis-configured entry can
    /// hand us a URL). An empty working directory just means "inherit ours", which is a
    /// better outcome than an exception escaping a transition.
    /// </summary>
    private static string SafeDirectoryOf(string exePath)
    {
        try { return Path.GetDirectoryName(exePath) ?? ""; }
        catch { return ""; }
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [StructLayout(LayoutKind.Sequential)]
    struct STARTUPINFO
    {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars;
        public int dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(
        string lpApplicationName, string lpCommandLine,
        IntPtr lpProcessAttributes, IntPtr lpThreadAttributes,
        bool bInheritHandles, int dwCreationFlags, IntPtr lpEnvironment,
        string lpCurrentDirectory, ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool CloseHandle(IntPtr hObject);
#endif
}
