using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private bool cohortObservationEnabled;

    private void SynchronizeCohortObservation() {
        // This changes only a worker-owned polling hint and never samples native state
        bool enabled = !ExitRequested && !finished && !cleanupRecovery && navigation.IsOpen
            && navigation.Surface == MenuSurface.Main && navigation.Page == MenuPage.AllPlayers;
        if (enabled == cohortObservationEnabled) return;
        session.SetAllPlayersGodmodeObservationEnabled(enabled);
        cohortObservationEnabled = enabled;
    }

    private bool HasCurrentCohortTarget(MenuAction action) {
        // The session still validates full membership and owned flags before any write
        // This earlier check keeps a replaced cached intent from starting a callback clock
        var current = session.ReadMenuState();
        return !current.Preview && current.Connected && !current.Busy && current.CanAllPlayersGodmode
            && current.AllPlayersGodmodeValue is "ON" or "OFF"
            && action.ExpectedCohort is not null
            && ReferenceEquals(action.ExpectedCohort, current.AllPlayersGodmodeTarget);
    }
}
