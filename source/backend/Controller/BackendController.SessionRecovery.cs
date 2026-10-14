namespace Apparition.Backend.Controller;

// A cleanup visit owns administrative navigation without borrowing a retired actor grant
public sealed partial class BackendController {
    // One visit distinguishes a failed cleanup retry from a deliberate Exit for retained changes
    private abstract record CleanupVisit;
    private sealed record FailedCleanupVisit : CleanupVisit;
    private sealed record OwnedCleanupVisit(object Owner) : CleanupVisit;
    private sealed record OfferedCleanupVisit(object Owner) : CleanupVisit;
    private CleanupVisit? cleanupVisit;
    private bool cleanupRecovery => cleanupVisit is FailedCleanupVisit or OwnedCleanupVisit;

    private void ObserveSessionRecovery() {
        if (finished || ExitRequested || cleanupVisit is FailedCleanupVisit) return;
        // The session supplies this explicit marker; disabled features alone cannot authorize recovery
        var state = session.ReadMenuState();
        if (state.NeedsSessionRecovery) EnterCleanupRecovery(state.LobbyAmmoStatus);
    }

    public void RetryAfterShutdownFailure(string? reason) {
        ObserveCompletion();
        EnterCleanupRecovery("Cleanup is incomplete. Select Exit to retry, or close Infinite Warfare. " + reason);
    }

    private void EnterCleanupRecovery(string reason) {
        // Withdraw stale account preferences before they can close the new actorless visit
        SynchronizeCommittedPreferences();
        // Counter admission precedes retiring the old callback and its visible error
        navigation.OpenCleanupRoot();
        CancelPreferenceRefresh();
        admittedMenuOwner = null;
        cleanupVisit = new FailedCleanupVisit();
        ExitRequested = false;
        retainedState = null;
        error = reason;
    }
}
