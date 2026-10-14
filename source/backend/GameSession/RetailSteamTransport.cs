using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using Memory;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// Keep the existing retail-game operations in one adapter
internal sealed partial class RetailSteamTransport : IRetailSessionTransport, syndishanx_iw_tool.Players.LocalObservation.ILocalActorSnapshotSource {
    private readonly Mem memory;
    private readonly object accessLock = new();
    private CheckedProcessMemory? checkedMemory;

    public RetailSteamTransport(Mem memory) {
        this.memory = memory;
    }

    public bool Connected {
        get {
            lock (accessLock) {
                try {
                    return memory.mProc?.Process is { HasExited: false } process
                        && process.ProcessName == "iw7_ship";
                } catch (Exception exception) when (exception is InvalidOperationException
                    or System.ComponentModel.Win32Exception or NotSupportedException) {
                    return false;
                }
            }
        }
    }

    // The foreground guard uses the identity of this one connection
    public int ProcessId {
        get {
            // Cleanup cannot release the process between the connection check and this identity read
            lock (accessLock) {
                return Connected ? memory.mProc.Process.Id : 0;
            }
        }
    }

    public PlayerState ReadTeammateState(int slot) {
        lock (accessLock) {
            var process = memory.mProc.Process;
            if (process.HasExited) {
                throw new IOException("The original game process has closed");
            }
            long moduleBase = process.MainModule?.BaseAddress.ToInt64() ?? 0;
            checkedMemory ??= new CheckedProcessMemory(process.Id);
            var current = PlayerStateReader.Read(moduleBase, slot,
                (address, count) => checkedMemory.Read(address, count));
            // An observed strict rollback also retires any already retained owned read access
            ownedTeammateAccess?.ObserveExisting(current);
            return current;
        }
    }

    // Opening remains the same native operation, with ownership held by the session
    public bool Open(int processId) {
        lock (accessLock) return memory.OpenProcess(processId);
    }

    public ITeammateFlagsTransport BindTeammate(PlayerIdentity selectedIdentity) {
        lock (accessLock) {
            var process = memory.mProc.Process;
            string executable = process.MainModule?.FileName
                ?? throw new IOException("The game executable could not be identified");
            using var file = File.OpenRead(executable);
            string hash = Convert.ToHexString(SHA256.HashData(file));
            // This player layout has been checked only against this installed retail build
            if (hash != "A883EE28EE9F552C63D8ED1E352F60501B2E256D8857CA178C6DD3AAE622723B") {
                throw new IOException("Teammate Godmode is unavailable for this unverified game build");
            }
            PlayerState initial = ReadTeammateState(selectedIdentity.Slot);
            if (initial.Identity != selectedIdentity) {
                throw new IOException("The selected teammate changed; refresh players and select again");
            }
            return BindOwnedTeammate(initial, process);
        }
    }

    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() {
        lock (accessLock) {
            if (!Connected) {
                return Array.Empty<DetectedPlayer>();
            }
            long moduleBase = memory.mProc.Process.MainModule?.BaseAddress.ToInt64() ?? 0;
            // Detection reads candidate name fields only and exposes no teammate writer
            var players = CandidateRosterReader.Read(moduleBase, (address, count) =>
                memory.ReadBytes(address.ToString("X", CultureInfo.InvariantCulture), count));
            return Connected ? players : Array.Empty<DetectedPlayer>();
        }
    }

    public async Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        if (!Connected) {
            return Array.Empty<long>();
        }
        // Preserve the upstream search rather than inventing another game target
        var candidates = await AmmoPatternDiscovery.FindOriginalAsync(
            pattern => memory.AoBScan(0x140000000, 0x150000000, pattern, false, true), token);
        token.ThrowIfCancellationRequested();
        return Connected ? candidates.ToArray() : Array.Empty<long>();
    }

    public byte[] ReadAmmoBytes(long candidate, int count) {
        lock (accessLock) {
            if (!Connected) {
                throw new IOException("The original Steam game has closed");
            }
            return memory.ReadBytes(candidate.ToString("X", CultureInfo.InvariantCulture), count)
                ?? throw new IOException("The ammo state could not be read");
        }
    }

    public bool WriteAmmoBytes(long candidate, byte[] bytes) {
        lock (accessLock) {
            if (!Connected) {
                return false;
            }
            // Use the bundled library and the original operation's byte width
            string value = string.Join(" ", bytes.Select(item =>
                item.ToString("X2", CultureInfo.InvariantCulture)));
            return memory.WriteMemory(candidate.ToString("X", CultureInfo.InvariantCulture),
                                      "bytes", value);
        }
    }

    public int ReadHealth() {
        lock (accessLock) {
            if (!Connected) {
                throw new IOException("The original Steam game has closed");
            }
            // Retain the upstream health location without treating failed reads as health zero
            var bytes = memory.ReadBytes("base+0x3D227F0", sizeof(int));
            if (bytes is null || bytes.Length != sizeof(int)) {
                throw new IOException("Health could not be read");
            }
            return BitConverter.ToInt32(bytes, 0);
        }
    }

    public bool WriteHealth(int value) {
        lock (accessLock) {
            return Connected && memory.WriteMemory("base+0x3D227F0", "int",
                value.ToString(CultureInfo.InvariantCulture));
        }
    }

    public void Dispose() {
        // Call only after feature operations and their background work have stopped
        lock (accessLock) {
            localObservationMemory?.Dispose();
            localObservationMemory = null;
            localObservationHeader = null;
            localObservationModuleBase = 0;
            localObservationBuildRejected = false;
            localObservationExecutable.Clear();
            if (memory.mProc is not null) {
                var process = memory.mProc.Process;
                memory.CloseProcess();
                // Release the separate checked native handle after every worker has stopped
                checkedMemory?.Dispose();
                checkedMemory = null;
                process?.Dispose();
            }
        }
    }
}
