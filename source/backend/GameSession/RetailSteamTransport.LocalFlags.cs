using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.GameSession;

internal sealed partial class RetailSteamTransport : ILocalGodmodeTransport {
    public LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken cancellationToken) =>
        ReadVerifiedLocalSnapshot(LocalActorFlagsReader.Read, cancellationToken);

    private LocalOwnedActorFlagsSnapshot ReadLocalOwnedActorFlagsSnapshot(CancellationToken cancellationToken) =>
        ReadVerifiedLocalSnapshot(LocalOwnedActorFlagsReader.Read, cancellationToken);

    // The caller owns feature lifetime; binding itself cannot change flags or start work
    public ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot selected) {
        ArgumentNullException.ThrowIfNull(selected);
        lock (accessLock) {
            var initial = ReadLocalActorFlagsSnapshot(CancellationToken.None);
            if (initial.Binding != selected.Binding || initial.CommandTime < selected.CommandTime
                || initial.Flags != selected.Flags) {
                throw new IOException("The local account or actor changed before flag binding");
            }
            var process = memory.mProc.Process;
            int lastCommandTime = initial.CommandTime;
            bool OriginalProcessConnected() {
                // Only the captured process lifetime can release a feature's pending restoration
                lock (accessLock) return !process.HasExited;
            }
            LocalOwnedActorFlagsSnapshot ReadOriginalActor() {
                lock (accessLock) {
                    if (!OriginalProcessConnected() || !Connected || !ReferenceEquals(memory.mProc.Process, process)) {
                        throw new IOException("The original connection is unavailable for local flag validation");
                    }
                    var current = ReadLocalOwnedActorFlagsSnapshot(CancellationToken.None);
                    if (!OriginalProcessConnected()) {
                        throw new IOException("The original connection is unavailable for local flag validation");
                    }
                    if (current.Binding != initial.Binding) {
                        throw new IOException("The original local actor binding changed during flag validation");
                    }
                    if (current.CommandTime < lastCommandTime) {
                        throw new IOException("The original local actor command time moved backwards during flag validation");
                    }
                    lastCommandTime = current.CommandTime;
                    return current;
                }
            }
            return new BoundLocalActorFlagsTransport(initial, OriginalProcessConnected, ReadOriginalActor,
                (address, expected, desired) => {
                    lock (accessLock) {
                        // Repeat the complete actor proof inside the writer's lock, after flag admission
                        var current = ReadOriginalActor();
                        if (address != initial.Binding.Identity.Entity + 0x1C8 || current.Flags != expected) return false;
                        // An owned OFF remains available after death; an ON still requires fresh normal play
                        if ((desired & 1U) != 0 && !current.IsEnableEligible) return false;
                        // This existing checked handle owns only the already opened original process
                        checkedMemory ??= new CheckedProcessMemory(process.Id);
                        return OriginalProcessConnected() && checkedMemory.WriteFlags(address, expected, desired);
                    }
                });
        }
    }
}
