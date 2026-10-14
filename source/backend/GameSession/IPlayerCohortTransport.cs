using syndishanx_iw_tool.Players;

namespace syndishanx_iw_tool.GameSession;

// A whole-cohort read preserves uncertain slots instead of reducing them to a roster
public interface IPlayerCohortTransport {
    PlayerCohortObservation ReadCohort();
}
