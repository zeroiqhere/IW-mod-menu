using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace syndishanx_iw_tool.GameSession;

// Native results and exact byte counts are required for player state checks
internal sealed class CheckedProcessMemory : IDisposable {
    private readonly SafeProcessHandle handle;

    public CheckedProcessMemory(int processId) {
        if (processId <= 0) {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }
        // Open the explicit memory rights instead of relying on a cached query handle
        handle = OpenProcess(0x0008 | 0x0010 | 0x0020, false, processId);
        if (handle.IsInvalid) {
            var error = new Win32Exception(Marshal.GetLastWin32Error());
            handle.Dispose();
            throw new IOException("The player memory handle could not be opened", error);
        }
    }

    public byte[] Read(long address, int count) {
        if (address < 0x10000 || count <= 0 || count > 65536) {
            throw new ArgumentOutOfRangeException(nameof(address));
        }
        byte[] bytes = new byte[count];
        if (!ReadProcessMemory(handle, (nint)address, bytes,
                (nuint)count, out nuint transferred) || transferred != (nuint)count) {
            throw new IOException("The player state read was incomplete",
                                  new Win32Exception(Marshal.GetLastWin32Error()));
        }
        return bytes;
    }

    public bool WriteFlags(long address, uint expected, uint desired) {
        // Only the Godmode bit may change, and another writer must not be overwritten knowingly
        if (address < 0x10000 || address % sizeof(uint) != 0 || (expected ^ desired) != 1
            || BitConverter.ToUInt32(Read(address, sizeof(uint))) != expected) {
            return false;
        }
        byte[] bytes = BitConverter.GetBytes(desired);
        return WriteProcessMemory(handle, (nint)address, bytes,
                   (nuint)bytes.Length, out nuint transferred) && transferred == (nuint)bytes.Length;
    }

    public void Dispose() => handle.Dispose();

    // The memory handle belongs to one immutable process connection
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access,
        [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address,
        [Out] byte[] buffer, nuint size, out nuint transferred);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(SafeProcessHandle process, nint address,
        byte[] buffer, nuint size, out nuint transferred);
}
