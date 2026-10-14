using System.IO;
using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

internal sealed partial class RetailSteamTransport : IPlayerCohortTransport {
    public PlayerCohortObservation ReadCohort() {
        lock (accessLock) {
            if (!Connected) throw new IOException("The original game process has closed");
            var process = memory.mProc.Process;
            long moduleBase = process.MainModule?.BaseAddress.ToInt64() ?? 0;
            checkedMemory ??= new CheckedProcessMemory(process.Id);
            // Membership reads share checked process access and never start a feature worker
            return PlayerStateReader.ReadCohort(moduleBase, process.Id,
                (address, count) => checkedMemory.Read(address, count));
        }
    }
}
