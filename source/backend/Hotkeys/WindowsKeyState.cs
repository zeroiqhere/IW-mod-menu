using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace syndishanx_iw_tool.Hotkeys;

// Reads desktop input state without sending input or opening another process
[SupportedOSPlatform("windows")]
internal static class WindowsKeyState
{
    // Windows virtual key codes for the two supported keypad bindings
    public const int Numpad1 = 0x61;
    public const int Numpad2 = 0x62;

    public static bool IsDown(int virtualKey)
    {
        // The high bit reports the current hold state and avoids the unreliable press bit
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    public static bool IsForegroundProcess(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        // Focus can briefly have no owner while windows change activation
        IntPtr foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero)
        {
            return false;
        }

        uint threadId = GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);

        // An invalid window or a different process cannot allow an in-game binding
        return threadId != 0 && foregroundProcessId == (uint)processId;
    }

    // Resolve the operating system library from its system directory
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetForegroundWindow();

    // The process identifier comes from window metadata without process access
    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
