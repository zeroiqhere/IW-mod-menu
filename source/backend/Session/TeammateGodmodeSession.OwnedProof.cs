using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Features;

namespace Apparition.Backend.Session;

public sealed partial class TeammateGodmodeSession {
    private readonly Func<PlayerIdentity, OwnedPlayerFlagsState>? readOwned;
    private readonly object proofGate = new();
    private readonly Dictionary<PlayerIdentity, int> commandTimes = new();
    private readonly HashSet<PlayerIdentity> retired = new();

    internal ITeammateFlagsTransport GuardOwnedTransport(PlayerIdentity identity, ITeammateFlagsTransport transport) {
        ArgumentNullException.ThrowIfNull(transport);
        if (readOwned is null) return transport;
        // Each proof read ends before the bound adapter takes its own access lock
        return new TeammateGodmodeOwnershipTransport(transport, enabling =>
            (!enabling || Volatile.Read(ref closing) == 0) && MatchesRetainedIdentity(identity)
            && (!enabling || MatchesCurrentIdentity(identity)));
    }

    internal bool MatchesRetainedIdentity(PlayerIdentity identity) {
        if (readOwned is null) return MatchesCurrentIdentity(identity);
        lock (proofGate) {
            if (!ValidPayload(identity.Slot, identity.Name) || retired.Contains(identity)) return false;
            try {
                OwnedPlayerFlagsState state = readOwned(identity)
                    ?? throw new InvalidOperationException("The owned teammate state is missing");
                if (state.Identity != identity || !ObserveCommandTime(identity, state.CommandTime))
                    throw new InvalidOperationException("The original teammate identity or time changed");
                // Eligibility is intentionally not an OFF or shutdown requirement
                return true;
            } catch (Exception) {
                // A recovered read cannot revive a writer after ownership proof failed
                RetireIdentity(identity);
                return false;
            }
        }
    }

    private bool ObserveCommandTime(PlayerIdentity identity, int commandTime) {
        if (retired.Contains(identity) || commandTime < 0
            || commandTimes.TryGetValue(identity, out int previous) && commandTime < previous) {
            RetireIdentity(identity);
            return false;
        }
        commandTimes[identity] = commandTime;
        return true;
    }
}
