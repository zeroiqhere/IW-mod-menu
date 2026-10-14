using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// All bytes and player flags belong to this test instance
internal sealed class RecordingRetailTransport : IRetailSessionTransport, ILocalGodmodeTransport, IOwnedTeammateStateTransport {
    private readonly object gate = new();
    private byte[] ammo = [0x01, 0x7C, 0x90, 0x14, 0x48, 0x8B, 0x4C, 0x24, 0x60];
    private readonly Dictionary<int, PlayerState> states = Enumerable.Range(0, 4).ToDictionary(slot => slot,
        slot => new PlayerState(new(slot, 0x100000 + slot * 0x1000, 0x200000 + slot * 0x1000,
            0x300000, 4, slot == 0 ? "Host" : "Friend" + slot, "000000000000000" + slot) { ServerClient = 0x300000 + slot * 715416L }, 100, 100, 0x200));
    private bool opened;
    public bool OpenResult = true;
    public int ReportedPid = 4242;
    public bool DisposeThrows;
    public bool RejectFlagRestore;
    public bool RejectAmmoWrites = false;
    public int Health = 100;
    public int OpenCalls;
    public int DisposeCalls;
    public int HealthWrites;
    public int AmmoWrites;
    public int AmmoReadCalls;
    public int FlagWrites;
    public bool Disposed;
    public Action? OnOpen;
    public Action? OnConnected;
    public Action? OnHealthRead = null;
    public Action? OnFlagRead = null;
    public Action? OnAmmoRead = null;
    public Func<CancellationToken, Task<IReadOnlyList<long>>>? Scan;

    public int LocalSlot;
    public ulong LocalAccountId = 123;
    public int LocalSessionState = 0;
    public int LocalCommandTime = 1000;
    public int LocalMovementType = 0;
    public bool FailLocalRead;
    public bool RejectLocalFlagRestore;
    public int LocalBindCalls;
    public int LocalReadCalls;
    public int TeammateBindCalls;
    public int LocalFlagWrites;
    public Action? OnLocalRead;
    public Action? OnLocalFlagRead = null;

    // Local account and actor facts come from owned fields, never slot selection heuristics
    private LocalActorSnapshot ReadLocalActor() {
        var state = ReadTeammateState(LocalSlot);
        var binding = new LocalAccountSessionBinding(0, LocalAccountId, 0x60000, 1,
            0x70000, LocalSlot, state.Identity);
        return new(binding, Volatile.Read(ref Health), LocalSessionState, true);
    }

    public LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Interlocked.Increment(ref LocalReadCalls);
        OnLocalRead?.Invoke();
        if (FailLocalRead) throw new IOException("Owned local source unavailable");
        var actor = ReadLocalActor();
        return new(actor, LocalCommandTime, LocalMovementType, Flags(actor.Binding.MemberIndex));
    }

    public ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot selected) {
        Interlocked.Increment(ref LocalBindCalls);
        return new BoundLocalActorFlagsTransport(selected, () => Connected, () => {
            // The actual bound adapter owns permanent retirement and living versus restore admission
            OnLocalFlagRead?.Invoke();
            var actor = ReadLocalActor();
            return new LocalOwnedActorFlagsSnapshot(actor, LocalCommandTime, LocalMovementType,
                Flags(actor.Binding.MemberIndex));
        }, (address, expected, desired) => {
            lock (gate) {
                int slot = selected.Binding.MemberIndex;
                var current = states[slot];
                if (address != selected.Binding.Identity.Entity + 0x1C8
                    || current.Identity != selected.Binding.Identity || current.Flags != expected
                    || RejectLocalFlagRestore && (desired & 1U) == 0) return false;
                states[slot] = current with { Flags = desired };
                Interlocked.Increment(ref LocalFlagWrites);
                Interlocked.Increment(ref FlagWrites);
                return true;
            }
        });
    }

    public bool Connected {
        get {
            bool result = opened && !Disposed;
            OnConnected?.Invoke();
            return result;
        }
    }
    public int ProcessId => opened && !Disposed ? ReportedPid : 0;
    public bool Open(int processId) {
        Interlocked.Increment(ref OpenCalls);
        OnOpen?.Invoke();
        opened = OpenResult;
        return OpenResult;
    }
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) =>
        Scan?.Invoke(token) ?? Task.FromResult<IReadOnlyList<long>>([0x140001000]);
    public byte[] ReadAmmoBytes(long candidate, int count) {
        lock (gate) {
            // Confirmation and original cleanup use the same owned instruction reader
            Interlocked.Increment(ref AmmoReadCalls);
            OnAmmoRead?.Invoke();
            return ammo.Take(count).ToArray();
        }
    }
    public bool WriteAmmoBytes(long candidate, byte[] bytes) {
        lock (gate) {
            Interlocked.Increment(ref AmmoWrites);
            if (RejectAmmoWrites) return false;
            bytes.CopyTo(ammo, 0);
            return true;
        }
    }
    public int ReadHealth() { OnHealthRead?.Invoke(); return Volatile.Read(ref Health); }
    public bool WriteHealth(int value) { Interlocked.Increment(ref HealthWrites); Volatile.Write(ref Health, value); return true; }
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() {
        // The owned roster includes every stored player rather than omitting slot three
        lock (gate) return states.OrderBy(entry => entry.Key)
            .Select(entry => new DetectedPlayer(entry.Key, entry.Value.Identity.Name)).ToArray();
    }
    public PlayerState ReadTeammateState(int slot) { lock (gate) return states[slot]; }
    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity identity) {
        lock (gate) {
            PlayerState state = states[identity.Slot];
            if (!Connected || state.Identity != identity) throw new IOException("The original teammate changed");
            return new(state.Identity, state.CommandTime, state.Flags, state.Health is > 1 and <= 10000);
        }
    }
    public uint Flags(int slot) => ReadTeammateState(slot).Flags;
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) {
        Interlocked.Increment(ref TeammateBindCalls);
        return new RecordingFlagsAdapter(this, new BoundPlayerFlagsTransport(
        identity, ReadTeammateState(identity.Slot), () => Connected, () => ReadOwnedTeammateFlags(identity),
        (address, expected, desired) => {
            lock (gate) {
                var current = states[identity.Slot];
                if (address != identity.Entity + 0x1C8 || current.Identity != identity || current.Flags != expected
                    || (desired & 1U) != 0 && current.Health is <= 1 or > 10000
                    || RejectFlagRestore && (desired & 1) == 0) return false;
                states[identity.Slot] = current with { Flags = desired };
                Interlocked.Increment(ref FlagWrites);
                return true;
            }
        }));
    }
    public void SetFlags(int slot, uint flags) {
        lock (gate) states[slot] = states[slot] with { Flags = flags };
    }
    public void SetPlayerState(int slot, PlayerState state) {
        ArgumentNullException.ThrowIfNull(state);
        if (slot is < 0 or > 3 || state.Identity.Slot != slot) {
            throw new ArgumentException("The owned state must keep its selected slot", nameof(state));
        }
        // Replacement facts belong only to this fixture, never to a game reader
        lock (gate) states[slot] = state;
    }
    private sealed class RecordingFlagsAdapter(RecordingRetailTransport owner, ITeammateFlagsTransport bound) : ITeammateFlagsTransport {
        public bool Connected => bound.Connected;
        // Holding one owned read leaves later state checks available to other test workers
        public uint ReadFlags() { owner.OnFlagRead?.Invoke(); return bound.ReadFlags(); }
        public bool WriteFlags(uint expected, uint desired) => bound.WriteFlags(expected, desired);
    }
    public void Dispose() {
        Interlocked.Increment(ref DisposeCalls);
        if (DisposeThrows) throw new IOException("Owned disposal refused");
        Disposed = true;
    }
}
