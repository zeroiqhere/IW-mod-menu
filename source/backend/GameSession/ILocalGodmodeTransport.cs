using syndishanx_iw_tool.Features;
using syndishanx_iw_tool.Players.LocalObservation;

namespace syndishanx_iw_tool.GameSession;

// Only a verified local actor can create a personal flag owner on this connection
public interface ILocalGodmodeTransport {
    LocalActorFlagsSnapshot ReadLocalActorFlagsSnapshot(CancellationToken cancellationToken);
    ITeammateFlagsTransport BindLocalActor(LocalActorFlagsSnapshot selected);
}
