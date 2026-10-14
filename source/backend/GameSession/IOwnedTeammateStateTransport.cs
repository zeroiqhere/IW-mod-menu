using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// This optional read capability never selects another actor or creates a writer
public interface IOwnedTeammateStateTransport {
    OwnedPlayerFlagsState ReadOwnedTeammateFlags(PlayerIdentity expectedIdentity);
}
