using Apparition.Backend.Controller;
using Apparition.Backend.OpenControls;
using Apparition.Backend.Preferences;
using Apparition.Backend.Session;
using Backend.Tests;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players.LocalObservation;
using syndishanx_iw_tool.SessionLifecycle;

namespace Session.Tests;

internal static class RecoveryControllerTests {
    public static async Task ActualControllerReturnsThroughPlayersAndTurnsOffOnlyTheOriginalOwner() {
        await using var rig = new RecoveryControllerRig();
        await rig.OpenAsync();
        await rig.SelectLabelAsync("Players Menu");
        await rig.SelectLabelAsync("Refresh players");
        await rig.SelectLabelAsync(PlayerStateMemoryFixture.Name(1));

        PlayerMenuTarget original = rig.Session.ReadMenuState().SelectedPlayerTarget
            ?? throw new InvalidOperationException("The production player selection reader did not publish a target");
        var selectedOptions = OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, rig.Session.ReadMenuState()).Rows
            .Single(row => row.Label == "Basic Scripts");
        RetailBackendSessionTests.Check(selectedOptions.Enabled
            && selectedOptions.Action.ExpectedTarget?.Equals(original) == true,
            "The actual player controller did not retain the selected target in its Options entry");
        await rig.SelectLabelAsync("Basic Scripts");
        await rig.SelectLabelAsync("God Mode");
        RetailBackendSessionTests.Check(rig.Transport.Flags(1) == 0x201,
            "The actual controller did not enable the selected teammate through its real memory reader");
        int strictReadsAfterOn = rig.Transport.StrictReadCalls(1);

        rig.Transport.SetSessionState(1, 1);
        rig.Back();
        rig.Back();
        RetailBackendSessionTests.Check(rig.Controller.Snapshot().Rows.Any(row => row.Label == "Refresh players"),
            "Two scoped Back commands did not return from Basic Scripts to Players");
        await rig.SelectLabelAsync("Refresh players");
        var recovery = OverlayMenuBuilder.Build(MenuPage.Players, rig.Session.ReadMenuState()).Rows
            .Single(row => row.Label == "Return to cleanup: " + original.Identity.Name);
        RetailBackendSessionTests.Check(recovery.Enabled
            && recovery.Action.Page == MenuPage.SelectedPlayer
            && recovery.Action.ExpectedTarget?.Equals(original) == true
            && !OverlayMenuBuilder.Build(MenuPage.Players, rig.Session.ReadMenuState()).Rows.Any(row =>
                row.Action.Kind == MenuActionKind.SelectPlayer && row.Action.Slot == original.Identity.Slot),
            "The current Players rows did not expose only the original owned cleanup route");

        await rig.SelectLabelAsync(recovery.Label);
        var reopenedOptions = rig.Controller.Snapshot();
        var reopenedBasic = OverlayMenuBuilder.Build(MenuPage.SelectedPlayer, rig.Session.ReadMenuState()).Rows
            .Single(row => row.Label == "Basic Scripts");
        RetailBackendSessionTests.Check(reopenedOptions.Rows.Single(row => row.Label == "Basic Scripts").Enabled
            && reopenedBasic.Action.ExpectedTarget?.Equals(original) == true
            && rig.Session.ReadMenuState().SelectedPlayerTarget?.Equals(original) == true,
            "The controller recovery row did not reopen the exact original player's Options");
        await rig.SelectLabelAsync("Basic Scripts");
        var off = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, rig.Session.ReadMenuState()).Rows
            .Single(row => row.Label == "God Mode");
        RetailBackendSessionTests.Check(off.Enabled && off.Value == "ON"
            && off.Action.ExpectedTarget?.Equals(original) == true,
            "Reopened Basic Scripts lost the original private OFF action");
        await rig.SelectLabelAsync("God Mode");

        RetailBackendSessionTests.Check(rig.Transport.Flags(1) == 0x200
            && rig.Transport.FlagWrites == 2
            && rig.Transport.StrictReadsAtLastFlagWrite(1) == strictReadsAfterOn
            && rig.Transport.TeammateBindCalls == 1 && rig.Transport.HealthWrites == 0,
            "The controller OFF changed another owner or depended on strict selection or health writes");
        rig.Back();
        rig.Back();
        await rig.SelectLabelAsync("Refresh players");
        var cleaned = rig.Session.ReadMenuState();
        var cleanedPlayers = rig.Controller.Snapshot();
        var unavailableGodmode = OverlayMenuBuilder.Build(MenuPage.SelectedBasicScripts, cleaned).Rows
            .Single(row => row.Label == "God Mode");
        RetailBackendSessionTests.Check(cleaned.SelectedPlayerTarget is null
            && !cleaned.CanRecoverSelectedTeammate
            && !cleanedPlayers.Rows.Any(row => row.Label.StartsWith("Return to cleanup:", StringComparison.Ordinal))
            && !unavailableGodmode.Enabled && unavailableGodmode.Value == "UNKNOWN"
            && rig.Transport.Flags(1) == 0x200 && rig.Transport.TeammateBindCalls == 1,
            "A confirmed OFF left a cleanup row or restored ON eligibility after refresh");
    }

    public static async Task ActualControllerRejectsRecoveryClickWhenItsFreshTargetWasReplaced() {
        await using var rig = new RecoveryControllerRig();
        await rig.OpenAsync();
        await rig.SelectLabelAsync("Players Menu");
        await rig.SelectLabelAsync("Refresh players");
        await rig.SelectLabelAsync(PlayerStateMemoryFixture.Name(1));
        PlayerMenuTarget original = rig.Session.ReadMenuState().SelectedPlayerTarget
            ?? throw new InvalidOperationException("The production player selection reader did not publish a target: "
                + string.Join(", ", rig.Controller.Snapshot().Rows.Select(row => row.Label + ":" + row.Enabled))
                + $" selected={rig.Session.ReadMenuState().SelectedSlot}/{rig.Session.ReadMenuState().SelectedName}"
                + $" local={rig.Session.ReadLocalMenuAdmission()?.Revision} seq={rig.Controller.LastSequence}");
        await rig.SelectLabelAsync("Basic Scripts");
        await rig.SelectLabelAsync("God Mode");
        RetailBackendSessionTests.Check(rig.Transport.Flags(1) == 0x201 && rig.Transport.TeammateBindCalls == 1,
            "The stale-route test did not establish one original active owner");

        rig.Transport.SetSessionState(1, 1);
        rig.Back();
        rig.Back();
        await rig.SelectLabelAsync("Refresh players");
        _ = rig.Controller.Snapshot();
        var staleRecovery = OverlayMenuBuilder.Build(MenuPage.Players, rig.Session.ReadMenuState()).Rows
            .Single(row => row.Label == "Return to cleanup: " + original.Identity.Name).Action;
        RetailBackendSessionTests.Check(staleRecovery.ExpectedTarget?.Equals(original) == true,
            "The controller did not publish the expected private recovery target before replacement");

        void ReplaceAfterCapturedState() {
            rig.ControllerSession.AfterNextStateRead(() => {
                rig.Transport.SetGuid(original.Identity.Slot, "ABCDEF0123456789");
                rig.Session.ExecuteAsync(new(MenuActionKind.RefreshPlayers)).GetAwaiter().GetResult();
            });
        }
        await rig.SelectLabelAsync("Return to cleanup: " + original.Identity.Name,
            beforeDispatch: ReplaceAfterCapturedState);

        var afterReplacement = rig.Controller.Snapshot();
        var state = rig.Session.ReadMenuState();
        RetailBackendSessionTests.Check(afterReplacement.Title == "Players Menu"
            && afterReplacement.Rows.Any(row => row.Label == "Refresh players")
            && !afterReplacement.Rows.Any(row => row.Label == "Basic Scripts")
            && state.SelectedPlayerTarget is null && !state.CanRecoverSelectedTeammate
            && rig.Transport.Flags(1) == 0x201 && rig.Transport.FlagWrites == 1
            && rig.Transport.TeammateBindCalls == 1 && rig.Transport.HealthWrites == 0,
            "A stale recovery click navigated to blank Options or rebound/wrote a replacement actor: "
                + $"title={afterReplacement.Title} rows={string.Join(",", afterReplacement.Rows.Select(row => row.Label))} "
                + $"target={state.SelectedPlayerTarget is not null} recovery={state.CanRecoverSelectedTeammate} "
                + $"flags={rig.Transport.Flags(1):X} writes={rig.Transport.FlagWrites} binds={rig.Transport.TeammateBindCalls}");
    }
}
