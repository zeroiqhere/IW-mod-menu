using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    private static string FormatGodmodeValue(GodmodeValue? value) => value switch {
        GodmodeValue.On => "ON", GodmodeValue.Off => "OFF", _ => "UNKNOWN"
    };

    private async Task CapturePersonalGodmodeAsync(RetailConnection owner) {
        if (!CanCommit(owner) || owner.LocalGodmode is null) return;
        await owner.CapturePersonalGodmodeAsync();
        var view = owner.LocalGodmode.ReadView();
        await CommitAsync(owner, () => personalStatus = view.Status);
    }

    private async Task ToggleGodmodeAsync(RetailConnection owner, MenuAction action) {
        var service = owner.LocalGodmode;
        if (service is null || !service.ReadView().HasBoundTarget || !CanCommit(owner)) return;
        // A selected-player action cannot silently become a different personal target
        if (action.Slot >= 0) {
            var selected = Volatile.Read(ref playerSelection)?.Target?.Identity;
            var current = service.ReadView().Identity;
            if (selected is null || selected != current || selected.Slot != action.Slot
                || !StringComparer.Ordinal.Equals(selected.Name, action.ExpectedName)) return;
        }
        var identity = service.ReadView().Identity;
        if (identity is null) return;
        var result = await owner.GodmodeOwners.ToggleAsync(identity);
        var view = service.ReadView();
        // A confirmed OFF refreshes only eligibility, so the same menu row can enable again
        if (result.Succeeded && view.Value == GodmodeValue.Off && CanCommit(owner) && foregroundAllowed()) {
            await owner.CapturePersonalGodmodeAsync();
            view = service.ReadView();
        }
        await CommitAsync(owner, () => {
            personalStatus = view.Status;
            // The registry result keeps its failure detail when the local view changes during rollback
            Log(result.Succeeded ? "Your Godmode: " + personalStatus
                : "Your Godmode: " + FormatGodmodeValue(view.Value) + " (toggle failed: "
                    + (result.FailureReason ?? "The toggle was not confirmed") + ")");
        });
        await RefreshCohortGodmodeAsync(owner);
    }

    private async Task RestoreGodmodeAsync(RetailConnection owner) {
        if (owner.LocalGodmode is null || !owner.LocalGodmode.ReadView().HasBoundTarget || !CanCommit(owner)) return;
        // This route never turns an unavailable ON into a guessed new enable
        var identity = owner.LocalGodmode.ReadView().Identity;
        if (identity is null) return;
        var result = await owner.GodmodeOwners.SetAsync(identity, false);
        var view = owner.LocalGodmode.ReadView();
        await CommitAsync(owner, () => {
            personalStatus = view.Status;
            // A failed restore can retain ON; its cached view does not contain the operation outcome
            Log(result.Succeeded ? "Restore your Godmode: " + personalStatus
                : "Restore your Godmode: " + FormatGodmodeValue(view.Value) + " (restoration failed: "
                    + (result.FailureReason ?? "Restoration was not confirmed") + ")");
        });
        await RefreshCohortGodmodeAsync(owner);
    }

    private async Task RefreshPlayersAsync(RetailConnection owner) {
        syndishanx_iw_tool.Players.DetectedPlayer[] observed;
        try {
            observed = owner.Transport.ReadPlayerNames().ToArray();
        } catch (Exception error) {
            observed = Array.Empty<syndishanx_iw_tool.Players.DetectedPlayer>();
            await CommitAsync(owner, () => Log("Player detection failed: " + error.Message));
        }

        // A missing display candidate can keep only an existing teammate cleanup route
        SelectedGodmodeView? teammateView = null;
        try {
            await owner.Teammates.RefreshAsync();
            teammateView = owner.Teammates.ReadView();
        } catch {
            // A route is withdrawn unless this refresh confirms the original owned identity
        }
        await CommitAsync(owner, () => {
            players = observed;
            ClearMissingSelection(observed, teammateView);
        });
        // Refresh can recover same-actor eligibility without starting an enable or reading on a frame
        if (CanCommit(owner)) await CapturePersonalGodmodeAsync(owner);
        await RefreshCohortGodmodeAsync(owner);
    }
}
