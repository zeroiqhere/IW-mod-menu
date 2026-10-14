using System.Diagnostics;
using System.Runtime.InteropServices;
using Apparition.Backend.Runtime;

namespace syndishanx_iw_tool.Overlay;

public sealed record GameWindow(int ProcessId, nint Handle);

// Window metadata identifies the exact game without opening its memory
internal static class ForegroundGame {
    public static bool OtherToolIsRunning() {
        foreach (string name in OtherToolPolicy.ProcessNames) {
            var processes = Process.GetProcessesByName(name);
            try {
                // Separate tools cannot safely share ownership of the same patched bytes
                if (processes.Any(process => OtherToolPolicy.IsConflict(name, process.Id, Environment.ProcessId)
                    && !process.HasExited)) {
                    return true;
                }
            } finally {
                foreach (var process in processes) {
                    process.Dispose();
                }
            }
        }
        return false;
    }

    public static GameWindow? Read() {
        nint window = GetForegroundWindow();
        if (window == 0 || GetWindowThreadProcessId(window, out uint pid) == 0
            || pid == 0 || pid > int.MaxValue) {
            return null;
        }
        return IsAlive((int)pid) ? new GameWindow((int)pid, window) : null;
    }

    public static bool IsAlive(int pid) {
        try {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited && string.Equals(process.ProcessName, "iw7_ship",
                StringComparison.OrdinalIgnoreCase);
        } catch (ArgumentException) {
            return false;
        } catch (InvalidOperationException) {
            return false;
        } catch (System.ComponentModel.Win32Exception) {
            return false;
        }
    }

    public static int FindSingleGame() {
        var games = Process.GetProcessesByName("iw7_ship");
        try {
            // Multiple instances cannot be resolved from a display name
            return games.Length == 1 && !games[0].HasExited ? games[0].Id : 0;
        } catch (InvalidOperationException) {
            return 0;
        } catch (System.ComponentModel.Win32Exception) {
            return 0;
        } finally {
            foreach (var game in games) {
                game.Dispose();
            }
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetForegroundWindow();

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
