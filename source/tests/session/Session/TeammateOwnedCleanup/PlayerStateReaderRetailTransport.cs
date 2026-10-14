using System.Buffers.Binary;
using Backend.Tests;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;

namespace Session.Tests;

// This transport exercises the production readers over a sparse, owned byte fixture
internal sealed class PlayerStateReaderRetailTransport : IRetailSessionTransport, IOwnedTeammateStateTransport {
    private readonly object gate = new();
    private readonly PlayerStateMemoryFixture memory = new();
    private readonly int[] strictReadCalls = new int[4];
    private readonly int[] ownedReadCalls = new int[4];
    private readonly int[] strictReadsAtLastFlagWrite = new int[4];
    private bool opened;
    private bool disposed;
    private int openedProcessId;

    internal int TeammateBindCalls;
    internal int FlagWrites;
    internal int HealthWrites;
    internal Action<OwnedPlayerFlagsState>? AfterOwnedRead;

    public bool Connected => Volatile.Read(ref opened) && !Volatile.Read(ref disposed);
    public int ProcessId => Connected ? Volatile.Read(ref openedProcessId) : 0;

    public bool Open(int processId) {
        Volatile.Write(ref openedProcessId, processId);
        Volatile.Write(ref opened, processId == 4242);
        return Connected;
    }

    public PlayerState ReadTeammateState(int slot) {
        Interlocked.Increment(ref strictReadCalls[slot]);
        lock (gate) return PlayerStateReader.Read(PlayerStateMemoryFixture.ModuleBase, slot, memory.ReadBytes);
    }

    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity expectedIdentity) {
        Interlocked.Increment(ref ownedReadCalls[expectedIdentity.Slot]);
        lock (gate) {
            OwnedPlayerFlagsState state = ReadOwnedFlags(expectedIdentity);
            // A transition after a checked sample exposes the next proof boundary
            AfterOwnedRead?.Invoke(state);
            return state;
        }
    }

    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) {
        Interlocked.Increment(ref TeammateBindCalls);
        PlayerState initial = ReadTeammateState(identity.Slot);
        return new BoundPlayerFlagsTransport(identity, initial, () => Connected,
            () => ReadOwnedTeammateFlags(identity), (address, expected, desired) => {
                lock (gate) {
                    OwnedPlayerFlagsState current = ReadOwnedFlags(identity);
                    if (address != identity.Entity + 0x1C8 || current.Identity != identity
                        || current.Flags != expected || (desired & 1U) != 0 && !current.IsEnableEligible) return false;
                    memory.Int32(address, unchecked((int)desired));
                    // Record the proof used at the write, before later publication can refresh strict state
                    Volatile.Write(ref strictReadsAtLastFlagWrite[identity.Slot], StrictReadCalls(identity.Slot));
                    Interlocked.Increment(ref FlagWrites);
                    return true;
                }
            });
    }

    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() {
        // Candidate names and owned cleanup proof share bytes but apply different eligibility rules
        lock (gate) return CandidateRosterReader.Read(PlayerStateMemoryFixture.ModuleBase, memory.ReadBytes);
    }

    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<long>>(Array.Empty<long>());
    }

    public byte[] ReadAmmoBytes(long candidate, int count) => throw new NotSupportedException();
    public bool WriteAmmoBytes(long candidate, byte[] bytes) => false;
    public int ReadHealth() => ReadTeammateState(0).Health;
    public bool WriteHealth(int value) { Interlocked.Increment(ref HealthWrites); return false; }

    internal int StrictReadCalls(int slot) => Volatile.Read(ref strictReadCalls[slot]);
    internal int OwnedReadCalls(int slot) => Volatile.Read(ref ownedReadCalls[slot]);

    internal int StrictReadsAtLastFlagWrite(int slot) => Volatile.Read(ref strictReadsAtLastFlagWrite[slot]);

    internal uint Flags(int slot) {
        // Inspect fixture bytes without borrowing an identity that the production reader has rejected
        lock (gate) {
            byte[] bytes = memory.ReadBytes(PlayerStateMemoryFixture.Entity(slot) + 0x1C8, sizeof(uint))
                ?? throw new InvalidOperationException("The fixture flags are unavailable");
            return BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        }
    }

    internal void SetSessionState(int slot, int value) {
        lock (gate) memory.Int32(PlayerStateMemoryFixture.Client(slot) + 0x4A94, value);
    }

    internal void SetConnectedState(int slot, int value) {
        lock (gate) memory.Int32(PlayerStateMemoryFixture.Client(slot) + 0x4A90, value);
    }

    internal void SetHealth(int slot, int health) {
        lock (gate) memory.Int32(PlayerStateMemoryFixture.Entity(slot) + 0x1E0, health);
    }

    internal void SetFlags(int slot, uint flags) {
        lock (gate) memory.Int32(PlayerStateMemoryFixture.Entity(slot) + 0x1C8, unchecked((int)flags));
    }

    // The name and slot remain unchanged while the server identity is replaced
    internal void SetGuid(int slot, string guid) {
        lock (gate) memory.Text(PlayerStateMemoryFixture.Server(slot) + 0x9F340, guid, 21);
    }

    private OwnedPlayerFlagsState ReadOwnedFlags(PlayerIdentity identity) =>
        PlayerStateReader.ReadOwnedFlags(PlayerStateMemoryFixture.ModuleBase, identity, memory.ReadBytes);

    public void Dispose() => Volatile.Write(ref disposed, true);
}
