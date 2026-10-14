using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.GameSession;
using syndishanx_iw_tool.Players;

namespace Session.Tests;

// This real interface boundary deliberately supplies no local actor flags capability
internal sealed class UnsupportedLocalRetailTransport(RecordingRetailTransport storage) : IRetailSessionTransport {
    public bool Connected => storage.Connected;
    public int ProcessId => storage.ProcessId;
    public bool Open(int processId) => storage.Open(processId);
    public Task<IReadOnlyList<long>> FindAmmoCandidatesAsync(CancellationToken cancellationToken) =>
        storage.FindAmmoCandidatesAsync(cancellationToken);
    public byte[] ReadAmmoBytes(long candidate, int count) => storage.ReadAmmoBytes(candidate, count);
    public bool WriteAmmoBytes(long candidate, byte[] bytes) => storage.WriteAmmoBytes(candidate, bytes);
    // Legacy methods exist on the port but must never be chosen as a personal fallback
    public int ReadHealth() => storage.ReadHealth();
    public bool WriteHealth(int value) => storage.WriteHealth(value);
    public PlayerState ReadTeammateState(int slot) => storage.ReadTeammateState(slot);
    public ITeammateFlagsTransport BindTeammate(PlayerIdentity identity) => storage.BindTeammate(identity);
    public IReadOnlyList<DetectedPlayer> ReadPlayerNames() => storage.ReadPlayerNames();
    public void Dispose() => storage.Dispose();
}
