using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalBinding;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Extra cohort ports belong only to this fixture; older transport tests keep their contracts
internal sealed class CohortRetailTransport : IRetailSessionTransport, ILocalGodmodeTransport,
    IOwnedTeammateStateTransport, ILocalActorSnapshotSource, IPlayerCohortTransport {
    internal RecordingRetailTransport Inner { get; } = new();
    internal bool Host = true;
    internal bool UnknownSlot;
    internal int ActorReads;
    internal int CohortReads;
    internal Action? OnCohortRead;

    public bool Connected => Inner.Connected;
    public int ProcessId => Inner.ProcessId;
    public bool Open(int processId) => Inner.Open(processId);
    public void Dispose() => Inner.Dispose();
    public PlayerState ReadTeammateState(int slot) => Inner.ReadTeammateState(slot);
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() => Inner.ReadPlayerNames();
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) => Inner.BindTeammate(identity);
    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity identity) => Inner.ReadOwnedTeammateFlags(identity);
    public LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken token) => Inner.ReadLocalActorFlagsSnapshot(token);
    public ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot snapshot) => Inner.BindLocalActor(snapshot);
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) => Inner.FindAmmoCandidatesAsync(token);
    public byte[] ReadAmmoBytes(long candidate, int count) => Inner.ReadAmmoBytes(candidate, count);
    public bool WriteAmmoBytes(long candidate, byte[] bytes) => Inner.WriteAmmoBytes(candidate, bytes);
    public int ReadHealth() => Inner.ReadHealth();
    public bool WriteHealth(int value) => Inner.WriteHealth(value);

    public LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken token) {
        token.ThrowIfCancellationRequested();
        Interlocked.Increment(ref ActorReads);
        var identity = Inner.ReadTeammateState(Inner.LocalSlot).Identity;
        var binding = new LocalAccountSessionBinding(0, Inner.LocalAccountId, 0x60000, 1,
            0x70000, Inner.LocalSlot, identity);
        return new(binding, Inner.Health, Inner.LocalSessionState, Host);
    }

    public PlayerCohortObservation ReadCohort() {
        Interlocked.Increment(ref CohortReads);
        OnCohortRead?.Invoke();
        var slots = Enumerable.Range(0, 4).Select(slot => {
            if (UnknownSlot && slot == 3)
                return new PlayerSlotObservation(slot, PlayerSlotPresence.Unknown, null, "Incomplete source slot");
            var state = Inner.ReadTeammateState(slot);
            // Fixture command times are copied from the same local owner used by its flag adapter
            return new PlayerSlotObservation(slot, PlayerSlotPresence.Present, new OwnedPlayerFlagsState(
                state.Identity, slot == Inner.LocalSlot ? Inner.LocalCommandTime : state.CommandTime,
                state.Flags, state.Health is > 1 and <= 10000));
        }).ToArray();
        return new(new(ProcessId, 0x140000000, Inner.ReadTeammateState(0).Identity.ServerClients, 4), slots);
    }
}
