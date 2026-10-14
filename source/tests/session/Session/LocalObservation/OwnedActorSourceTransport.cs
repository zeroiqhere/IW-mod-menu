using Backend.Tests;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Session.Tests;

// Real readers consume owned recorded bytes while legacy feature calls remain separately counted
internal sealed class OwnedActorSourceTransport : IRetailSessionTransport, ILocalActorSnapshotSource {
    internal readonly RecordingRetailTransport Features = new();
    internal LocalActorMemoryFixture Actor = new();
    internal int ActorReads;
    internal Action? BeforeActorRead;
    public bool Connected => Features.Connected;
    public int ProcessId => Features.ProcessId;
    public bool Open(int processId) => Features.Open(processId);

    public LocalActorSnapshot ReadLocalActorSnapshot(CancellationToken token) {
        ActorReads++;
        token.ThrowIfCancellationRequested();
        BeforeActorRead?.Invoke();
        return LocalActorSnapshotReader.Read(LocalBindingMemoryFixture.ModuleBase, (address, width) => {
            token.ThrowIfCancellationRequested();
            if (!Connected) throw new IOException("The owned source is closed");
            return Actor.Memory.ReadBytes(address, width);
        });
    }

    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken token) => Features.FindAmmoCandidatesAsync(token);
    public byte[] ReadAmmoBytes(long address, int count) => Features.ReadAmmoBytes(address, count);
    public bool WriteAmmoBytes(long address, byte[] bytes) => Features.WriteAmmoBytes(address, bytes);
    public int ReadHealth() => Features.ReadHealth();
    public bool WriteHealth(int value) => Features.WriteHealth(value);
    public PlayerState ReadTeammateState(int slot) => Features.ReadTeammateState(slot);
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) => Features.BindTeammate(identity);
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() => Features.ReadPlayerNames();
    public void Dispose() => Features.Dispose();
}
