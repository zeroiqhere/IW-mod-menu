using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace NativeObservation.Tests;

// This diagnostic compares read access masks on this fixture's own allocation only
internal static class AccessMaskDiagnostic {
    internal static int Run() {
        nint memory = Marshal.AllocHGlobal(4);
        try {
            Marshal.WriteInt32(memory, 1234);
            foreach (uint mask in new uint[] { 0x10, 0x1010, 0x410, 0x18, 0x38 }) {
                using var handle = OpenProcess(mask, false, Environment.ProcessId);
                var bytes = new byte[4];
                bool read = ReadProcessMemory(handle, memory, bytes, 4, out nuint copied);
                int readError = Marshal.GetLastWin32Error();
                bool written = WriteProcessMemory(handle, memory, BitConverter.GetBytes(1234), 4, out _);
                Console.WriteLine($"access=0x{mask:X} invalid={handle.IsInvalid} read={read} copied={copied} readError={readError} write={written} writeError={Marshal.GetLastWin32Error()}");
            }
            return 0;
        } finally { Marshal.FreeHGlobal(memory); }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeProcessHandle OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(SafeProcessHandle process, nint address, [Out] byte[] bytes, nuint count, out nuint transferred);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(SafeProcessHandle process, nint address, byte[] bytes, nuint count, out nuint transferred);
}
