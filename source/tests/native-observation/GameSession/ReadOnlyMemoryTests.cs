using System.ComponentModel;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using syndishanx_iw_tool.GameSession;

namespace NativeObservation.Tests;

// Every allocation and attempted write belongs to this test process, never the game
internal static class ReadOnlyMemoryTests {
    private static void Check(bool value, string reason) {
        if (!value) throw new InvalidOperationException(reason);
    }
    private static void Reject<T>(Action action) where T : Exception {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException("Expected " + typeof(T).Name);
    }

    public static void CurrentProcessHandleReadsExactCopiedOwnedBytes() {
        nint allocation = Marshal.AllocHGlobal(32);
        try {
            byte[] expected = Enumerable.Range(0, 32).Select(index => (byte)index).ToArray();
            Marshal.Copy(expected, 0, allocation, expected.Length);
            using var reader = new ReadOnlyProcessMemory(Environment.ProcessId);
            byte[] first = reader.Read(allocation.ToInt64(), expected.Length);
            Check(first.SequenceEqual(expected), "The copied native read differed from owned bytes");
            first[0] = 0xFF;
            Check(reader.Read(allocation.ToInt64(), expected.Length).SequenceEqual(expected),
                "A returned array altered the allocation or later read");
            Check(reader.Read(allocation.ToInt64() + 1, 1)[0] == 1, "An exact unaligned byte read was rejected");
        } finally { Marshal.FreeHGlobal(allocation); }
    }

    public static void ReadOnlyHandleRejectsAnAttemptedWriteToOwnedBytes() {
        nint allocation = Marshal.AllocHGlobal(4);
        try {
            Marshal.WriteInt32(allocation, 1234);
            using var reader = new ReadOnlyProcessMemory(Environment.ProcessId);
            var field = typeof(ReadOnlyProcessMemory).GetField("handle", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("The tested native handle was not found");
            var handle = (SafeProcessHandle)field.GetValue(reader)!;
            // Exercise the actual access mask rather than mocking a write refusal
            bool written = WriteProcessMemory(handle, allocation, BitConverter.GetBytes(5678), 4, out _);
            int error = Marshal.GetLastWin32Error();
            Check(!written && error == 5 && Marshal.ReadInt32(allocation) == 1234,
                "The observation handle allowed a write or changed owned bytes");
        } finally { Marshal.FreeHGlobal(allocation); }
    }

    public static void IncompleteProtectedAndReleasedPagesNeverReturnFabricatedBytes() {
        int pageSize = Environment.SystemPageSize;
        nint allocation = VirtualAlloc(0, (nuint)(pageSize * 2), 0x3000, 0x04);
        if (allocation == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        using var reader = new ReadOnlyProcessMemory(Environment.ProcessId);
        try {
            Marshal.WriteByte(allocation + pageSize - 1, 42);
            Check(VirtualProtect(allocation + pageSize, (nuint)pageSize, 0x01, out _), "Owned page protection failed");
            Check(reader.Read(allocation.ToInt64() + pageSize - 1, 1)[0] == 42, "The last readable byte differed");
            Reject<IOException>(() => reader.Read(allocation.ToInt64() + pageSize - 1, 2));
            Reject<IOException>(() => reader.Read(allocation.ToInt64() + pageSize, 1));
        } finally { Check(VirtualFree(allocation, 0, 0x8000), "Owned page release failed"); }
        Reject<IOException>(() => reader.Read(allocation.ToInt64(), 1));
    }

    public static void InvalidProcessIdsAndReadSpansRejectBeforeNativeReads() {
        Reject<ArgumentOutOfRangeException>(() => new ReadOnlyProcessMemory(0));
        Reject<ArgumentOutOfRangeException>(() => new ReadOnlyProcessMemory(-1));
        Reject<IOException>(() => new ReadOnlyProcessMemory(int.MaxValue));
        using var reader = new ReadOnlyProcessMemory(Environment.ProcessId);
        foreach (var span in new (long Address, int Count)[] {
            (0, 1), (0x10000, 0), (0x10000, -1), (0x10000, 4097),
            (0x7FFFFFFF0000, 2), (long.MaxValue, 4)
        }) {
            Reject<ArgumentOutOfRangeException>(() => reader.Read(span.Address, span.Count));
        }
    }

    public static void ReleasedHandleCannotReadAfterItsOwnerDisposesIt() {
        using var reader = new ReadOnlyProcessMemory(Environment.ProcessId);
        reader.Dispose();
        Reject<ObjectDisposedException>(() => reader.Read(0x10000, 1));
        reader.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteProcessMemory(SafeProcessHandle process, nint address, byte[] bytes,
        nuint count, out nuint transferred);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protection);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualProtect(nint address, nuint size, uint protection, out uint oldProtection);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);
}
