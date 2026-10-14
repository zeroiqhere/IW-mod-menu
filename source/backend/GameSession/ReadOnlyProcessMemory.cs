using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace syndishanx_iw_tool.GameSession;

// Observation uses its own read-only handle and exposes no game write operation
internal sealed class ReadOnlyProcessMemory : IDisposable {
    private readonly SafeProcessHandle handle;

    internal ReadOnlyProcessMemory(int processId) {
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        // PROCESS_VM_READ is sufficient for exact copied observations
        handle = OpenProcess(0x0010, false, processId);
        if (handle.IsInvalid) {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("The local observation handle could not be opened", error);
        }
    }

    internal byte[] Read(long address, int count) {
        const long maximumAddress = 0x7FFFFFFF0000;
        // Check the full span before converting its address to a native pointer
        if (address < 0x10000 || address > maximumAddress || count is <= 0 or > 4096
            || count - 1L > maximumAddress - address) {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        var bytes = new byte[count];
        if (!ReadProcessMemory(handle, (nint)address, bytes, (nuint)count, out nuint transferred)
            || transferred != (nuint)count) {
            throw new IOException("The local observation read was incomplete",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }
        return bytes;
    }

    public void Dispose() => handle.Dispose();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, nuint size, out nuint transferred);
}
