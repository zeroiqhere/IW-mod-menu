using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Native liveness and actor queries share the same lock as the retail transport
internal sealed class LockedRetailTransport : IRetailSessionTransport, ILocalGodmodeTransport, IOwnedTeammateStateTransport {
    private readonly object accessLock = new();
    private int holdActor;
    private int holdLiveness;
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public RecordingRetailTransport Storage { get; } = new() { LocalSlot = 0,
        FailLocalRead = false, OnLocalRead = null, RejectLocalFlagRestore = false };
    public int ConnectedReads;
    public int ProcessIdReads;
    public Task Entered => entered.Task;
    public void HoldNextActorQuery() => Interlocked.Exchange(ref holdActor, 1);
    public void HoldNextLivenessQuery() => Interlocked.Exchange(ref holdLiveness, 1);
    public void ReleaseQuery() => release.TrySetResult();

    private void Hold(ref int requested) {
        if (Interlocked.Exchange(ref requested, 0) == 0) return;
        // Waiting inside the access lock reproduces contention with Connected and ProcessId
        entered.TrySetResult();
        release.Task.GetAwaiter().GetResult();
    }

    public bool Connected {
        get { lock (accessLock) { Interlocked.Increment(ref ConnectedReads); Hold(ref holdLiveness); return Storage.Connected; } }
    }
    public int ProcessId {
        get { lock (accessLock) { Interlocked.Increment(ref ProcessIdReads); return Storage.ProcessId; } }
    }
    public bool Open(int processId) { lock (accessLock) return Storage.Open(processId); }
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) => Storage.FindAmmoCandidatesAsync(token);
    public byte[] ReadAmmoBytes(long candidate, int count) { lock (accessLock) return Storage.ReadAmmoBytes(candidate, count); }
    public bool WriteAmmoBytes(long candidate, byte[] bytes) { lock (accessLock) return Storage.WriteAmmoBytes(candidate, bytes); }
    public int ReadHealth() { lock (accessLock) return Storage.ReadHealth(); }
    public bool WriteHealth(int value) { lock (accessLock) return Storage.WriteHealth(value); }
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() { lock (accessLock) return Storage.ReadPlayerNames(); }
    public PlayerState ReadTeammateState(int slot) {
        lock (accessLock) { Hold(ref holdActor); return Storage.ReadTeammateState(slot); }
    }
    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity identity) {
        lock (accessLock) { Hold(ref holdActor); return Storage.ReadOwnedTeammateFlags(identity); }
    }
    public LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken token) {
        lock (accessLock) { Hold(ref holdActor); return Storage.ReadLocalActorFlagsSnapshot(token); }
    }
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) {
        lock (accessLock) return new LockedFlagsAdapter(this, Storage.BindTeammate(identity));
    }
    public ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot selected) {
        lock (accessLock) return new LockedFlagsAdapter(this, Storage.BindLocalActor(selected));
    }
    public void Dispose() { lock (accessLock) Storage.Dispose(); }

    // Bound preflight and monitor callbacks retain the same native access domain
    private sealed class LockedFlagsAdapter(LockedRetailTransport owner, ITeammateFlagsTransport bound) : ITeammateFlagsTransport {
        public bool Connected { get { lock (owner.accessLock) return bound.Connected; } }
        public uint ReadFlags() { lock (owner.accessLock) { owner.Hold(ref owner.holdActor); return bound.ReadFlags(); } }
        public bool WriteFlags(uint expected, uint desired) { lock (owner.accessLock) return bound.WriteFlags(expected, desired); }
    }
}
