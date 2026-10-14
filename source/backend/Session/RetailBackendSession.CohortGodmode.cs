using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    private bool cohortObservationWanted;

    // Navigation requests an owned worker read; a display frame never invokes a native reader
    public void SetAllPlayersGodmodeObservationEnabled(bool enabled) =>
        Volatile.Write(ref cohortObservationWanted, enabled && !closing);

    private static string FormatCohortGodmodeValue(GodmodeValue? value) => value switch {
        GodmodeValue.On => "ON", GodmodeValue.Off or GodmodeValue.Mixed => "OFF", _ => "UNKNOWN"
    };

    private async Task RefreshCohortGodmodeAsync(RetailConnection owner) {
        if (!CanCommit(owner)) return;
        await owner.CohortGodmode.RefreshAsync(owner.ObservationCancellation.Token).ConfigureAwait(false);
        // Group captures invalidate shared receipts, so peers refresh after that capture finishes
        if (CanCommit(owner)) await owner.Teammates.RefreshAsync(owner.ObservationCancellation.Token).ConfigureAwait(false);
    }

    private async Task ToggleCohortGodmodeAsync(RetailConnection owner, MenuAction action) {
        // A source all-player action cannot adopt a new cohort from its label or current slot list
        if (action.Page != MenuPage.AllPlayers || action.ExpectedCohort is null || !CanCommit(owner)) return;
        var result = await owner.CohortGodmode.ToggleAsync(action.ExpectedCohort).ConfigureAwait(false);
        if (CanCommit(owner)) await owner.Teammates.RefreshAsync(owner.ObservationCancellation.Token).ConfigureAwait(false);
        await CommitAsync(owner, () => Log("All Players Godmode: "
            + (result.Succeeded ? FormatCohortGodmodeValue(result.Snapshot.Value)
                : "Not confirmed: " + result.FailureReason))).ConfigureAwait(false);
    }
}
