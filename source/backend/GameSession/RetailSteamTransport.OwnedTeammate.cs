using System.IO;
using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

internal sealed partial class RetailSteamTransport : IOwnedTeammateStateTransport {
    private OwnedTeammateFlagsRegistry? ownedTeammateAccess;

    // Session proofs and bound adapters use the same original native read state
    public OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity expectedIdentity) {
        lock (accessLock) return OwnedTeammateAccess().Read(expectedIdentity);
    }

    private OwnedTeammateFlagsRegistry OwnedTeammateAccess() => ownedTeammateAccess ??= new(accessLock,
        () => Connected, ReadOwnedTeammateFlagsCore,
        (address, expected, desired) => Connected && checkedMemory!.WriteFlags(address, expected, desired));

    private OwnedPlayerFlagsState ReadOwnedTeammateFlagsCore(PlayerIdentity expectedIdentity) {
        // The caller already holds accessLock; this leaf avoids recursively entering the registry
        var process = memory.mProc.Process;
        if (process.HasExited) throw new IOException("The original game process has closed");
        long moduleBase = process.MainModule?.BaseAddress.ToInt64() ?? 0;
        checkedMemory ??= new CheckedProcessMemory(process.Id);
        return PlayerStateReader.ReadOwnedFlags(moduleBase, expectedIdentity,
            (address, count) => checkedMemory.Read(address, count));
    }

    private ITeammateFlagsTransport BindOwnedTeammate(PlayerState initial, System.Diagnostics.Process process) {
        // The final writer shares the watermark of all session proofs and preceding bound reads
        var access = OwnedTeammateAccess().Bind(initial);
        return new BoundPlayerFlagsTransport(initial.Identity, initial, () => !process.HasExited, access.Read, access.Write);
    }
}
