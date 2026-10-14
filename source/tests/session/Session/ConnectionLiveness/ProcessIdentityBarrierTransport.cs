using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// A held PID query belongs to the final process guard, not the bound actor adapter
internal sealed class ProcessIdentityBarrierTransport : IRetailSessionTransport, ILocalGodmodeTransport, IOwnedTeammateStateTransport {
    private int holdIdentity;
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public RecordingRetailTransport Storage { get; } = new();
    public Task Entered => entered.Task;
    public void HoldNextIdentityQuery() => Interlocked.Exchange(ref holdIdentity, 1);
    public void ReleaseQuery() => release.TrySetResult();
    public bool Connected => Storage.Connected;
    public int ProcessId {
        get {
            if (Interlocked.Exchange(ref holdIdentity, 0) != 0) {
                entered.TrySetResult();
                release.Task.GetAwaiter().GetResult();
            }
            return Storage.ProcessId;
        }
    }

    // The real recording adapters keep all original actor, flag, and byte checks
    public bool Open(int processId) => Storage.Open(processId);
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) => Storage.FindAmmoCandidatesAsync(token);
    public byte[] ReadAmmoBytes(long candidate, int count) => Storage.ReadAmmoBytes(candidate, count);
    public bool WriteAmmoBytes(long candidate, byte[] bytes) => Storage.WriteAmmoBytes(candidate, bytes);
    public int ReadHealth() => Storage.ReadHealth();
    public bool WriteHealth(int value) => Storage.WriteHealth(value);
    public PlayerState ReadTeammateState(int slot) => Storage.ReadTeammateState(slot);
    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity identity) => Storage.ReadOwnedTeammateFlags(identity);
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) => Storage.BindTeammate(identity);
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() => Storage.ReadPlayerNames();
    public LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken token) => Storage.ReadLocalActorFlagsSnapshot(token);
    public ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot selected) => Storage.BindLocalActor(selected);
    public void Dispose() => Storage.Dispose();
}
