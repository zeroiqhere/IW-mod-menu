using System.Diagnostics;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Runtime;

// Hold one process object and its start time so a recycled PID cannot retarget this session
internal sealed class GameIdentity : IDisposable {
    private readonly Process process;
    private readonly DateTime started;
    public int ProcessId { get; }

    public GameIdentity(int processId) {
        process = Process.GetProcessById(processId);
        try {
            if (process.HasExited || !string.Equals(process.ProcessName, "iw7_ship", StringComparison.OrdinalIgnoreCase)) {
                throw new InvalidOperationException("The specified process is not the retail Infinite Warfare game");
            }
            ProcessId = processId;
            started = process.StartTime;
        } catch {
            process.Dispose();
            throw;
        }
    }

    public bool IsAlive {
        get {
            try {
                return !process.HasExited && process.StartTime == started
                    && string.Equals(process.ProcessName, "iw7_ship", StringComparison.OrdinalIgnoreCase);
            } catch (Exception exception) when (exception is InvalidOperationException
                or System.ComponentModel.Win32Exception or NotSupportedException) {
                return false;
            }
        }
    }

    public bool IsForeground() => IsAlive && ForegroundGame.Read()?.ProcessId == ProcessId;
    public void Dispose() => process.Dispose();
}
