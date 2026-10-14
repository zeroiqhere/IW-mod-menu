using Apparition.Backend.Ipc;
using Apparition.Backend.Session;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // The original command retains private action targets and declaration roles without wire addresses
    private sealed record MonitorIntent(ulong Sequence, object Owner, ulong AdmissionRevision,
        LocalPlayerLife Life, PlayerIdentity ActorIdentity,
        MenuSurface Surface, MenuPage Page, ulong NavigationRevision, ulong MenuRevision, int SelectedIndex,
        IReadOnlyList<MenuRow> Rows, PlayerIdentity? PersonalIdentity, PlayerMenuTarget? SelectedTarget, CohortGodmodeTarget? CohortTarget, Func<bool> ForegroundAllowed);
    private sealed record MonitorResult(MenuSurface Surface, MenuPage Page, bool IsOpen,
        ulong NavigationRevision, ulong MenuRevision, int SelectedIndex, IReadOnlyList<MenuRow> Rows,
        PlayerIdentity? PersonalIdentity, PlayerMenuTarget? SelectedTarget, CohortGodmodeTarget? CohortTarget);
    private sealed record MonitorInvocation(MonitorIntent Intent, MenuMonitorContinuation Frame,
        MonitorResult Result, bool Bound, bool Expired);
    private MonitorInvocation? monitorInvocation;

    private bool AllowsMechanicalScope(CommandEnvelope command, Func<bool> foregroundAllowed) {
        if (command.MenuRevision is null) return true;
        var proof = session.ReadLocalMenuAdmission();
        var state = retainedState ?? session.ReadMenuState();
        // Older commands still use their existing guards but cannot acquire a new causal ticket
        return command.Input is OverlayInput.NextChoice or OverlayInput.PreviousChoice or OverlayInput.Back
                or OverlayInput.Close or OverlayInput.JumpBack
            && command.NavigationRevision == navigation.Revision && command.MenuRevision == navigation.MenuRevision
            && proof is not null && command.LocalAdmissionRevision == proof.Revision
            && admittedMenuOwner is not null && ReferenceEquals(admittedMenuOwner, proof.OwnerToken)
            && proof.Observation.ProcessId == processId && proof.Observation.HasMenu == true
            && proof.Observation.Life is LocalPlayerLife.Alive or LocalPlayerLife.Dead
            && !state.Preview && state.Connected && !Busy && !state.Busy && !MenuMonitorControlsDisabled
            && navigation.IsOpen && OrdinaryMonitorVisit && foregroundAllowed();
    }

    private MonitorIntent? CaptureMonitorIntent(CommandEnvelope? command, Func<bool>? foregroundAllowed,
        LocalMenuAdmissionSnapshot proof, OverlayMenuState state) {
        if (command?.MenuRevision is null || foregroundAllowed is null || proof.Observation.Identity is not { } actorIdentity || command.Sequence == 0
            || command.NavigationRevision != navigation.Revision || command.MenuRevision != navigation.MenuRevision
            || command.LocalAdmissionRevision != proof.Revision || Busy || state.Busy || MenuMonitorControlsDisabled
            || !foregroundAllowed()) return null;
        // One captured proof supplies the private owner and the wire admission revision
        var rows = Array.AsReadOnly(navigation.Rows.ToArray());
        // Empty pages can own navigation waits, but they have no row action target
        if (rows.Count == 0) {
            if (command.Input is not (OverlayInput.Back or OverlayInput.Close or OverlayInput.JumpBack)) return null;
        } else if (navigation.SelectedIndex < 0 || navigation.SelectedIndex >= rows.Count || rows[navigation.SelectedIndex].IsSpacer) return null;
        return new(command.Sequence, proof.OwnerToken, proof.Revision, proof.Observation.Life, actorIdentity, navigation.Surface, navigation.Page,
            navigation.Revision, navigation.MenuRevision, navigation.SelectedIndex, rows,
            PersonalMonitorIdentity(state), SelectedMonitorTarget(state), CohortMonitorTarget(state), foregroundAllowed);
    }

    // Unavailable actions may omit a target, so the selected visit also retains its private context
    private PlayerMenuTarget? SelectedMonitorTarget(OverlayMenuState state) =>
        navigation.Page is MenuPage.SelectedPlayer or MenuPage.SelectedBasicScripts ? state.SelectedPlayerTarget : null;
    private CohortGodmodeTarget? CohortMonitorTarget(OverlayMenuState state) =>
        navigation.Page == MenuPage.AllPlayers ? state.AllPlayersGodmodeTarget : null;
    private PlayerIdentity? PersonalMonitorIdentity(OverlayMenuState state) =>
        navigation.SelectedIndex >= 0 && navigation.SelectedIndex < navigation.Rows.Count
        && navigation.Rows[navigation.SelectedIndex].Action.Kind is MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
            ? state.PersonalGodmodeIdentity : null;

    private MonitorResult CaptureMonitorResult(OverlayMenuState state) => new(navigation.Surface, navigation.Page,
        navigation.IsOpen, navigation.Revision, navigation.MenuRevision, navigation.SelectedIndex,
        Array.AsReadOnly(navigation.Rows.ToArray()), PersonalMonitorIdentity(state), SelectedMonitorTarget(state), CohortMonitorTarget(state));

    private void CommitMonitorInvocation(LocalMenuMonitorWaitState prepared) {
        if (prepared.ContinuationIntent is not MonitorIntent intent) { CancelMonitorContinuation(); return; }
        // Only this command's own transition may supply the pending parent or closed result
        monitorInvocation = new(intent, new(prepared.Frame.Revision, intent.Sequence, intent.AdmissionRevision, prepared.Frame.Kind),
            CaptureMonitorResult(retainedState ?? session.ReadMenuState()), false, false);
    }

    private bool MonitorResultMatches(MonitorResult result, OverlayMenuState state) =>
        navigation.Surface == result.Surface && navigation.Page == result.Page && navigation.IsOpen == result.IsOpen
        && navigation.Revision == result.NavigationRevision && navigation.MenuRevision == result.MenuRevision
        && navigation.SelectedIndex == result.SelectedIndex && navigation.RowsMatchTopology(result.Rows)
        && PersonalMonitorIdentity(state) == result.PersonalIdentity
        && Equals(SelectedMonitorTarget(state), result.SelectedTarget)
        && ReferenceEquals(CohortMonitorTarget(state), result.CohortTarget);

    private MonitorInvocation? ValidateMonitorInvocation(OverlayMenuState state) {
        if (monitorInvocation is not { } invocation) return null;
        var proof = session.ReadLocalMenuAdmission();
        // A timer disappearing through cancellation never becomes a ready invocation
        if (finished || ExitRequested || cleanupRecovery || state.Preview || !state.Connected || !OrdinaryMonitorVisit
            || !MonitorResultMatches(invocation.Result, state) || !invocation.Intent.ForegroundAllowed()
            || proof is null || !ReferenceEquals(invocation.Intent.Owner, admittedMenuOwner)
            || !ReferenceEquals(invocation.Intent.Owner, proof.OwnerToken) || invocation.Intent.AdmissionRevision != proof.Revision
            || proof.Observation.Identity != invocation.Intent.ActorIdentity || proof.Observation.Life != invocation.Intent.Life
            || proof.Observation.ProcessId != processId || proof.Observation.HasMenu != true
            || proof.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)
            || optionCallback is not null || menuCredits is not null || widthEditor is not null && navigation.Page == MenuPage.WidthEditor
            || instructionPositionEditor is not null || menuPositionEditor is not null || menuUnlock.StartedTimestamp is not null
            || preferenceRefresh is not null || preferenceRebuild is not null || designTransition is not null || openTransition is not null
            || !invocation.Expired && (monitorWait is null || monitorWait.Frame.Revision != invocation.Frame.Revision)) {
            CancelMonitorContinuation();
            return null;
        }
        return invocation;
    }

    private MonitorInvocation? PrepareMonitorPublication(OverlayMenuState state) => ValidateMonitorInvocation(state);

    private MonitorInvocation? BindMonitorPublication(MonitorInvocation? prepared, OverlayMenuState state) {
        if (prepared is null) return null;
        // The first fresh row clamp belongs to the branch, not an assumed navigation increment
        if (prepared.Bound) return MonitorResultMatches(prepared.Result, state) ? prepared : null;
        return prepared with { Result = CaptureMonitorResult(state), Bound = true };
    }

    private MenuMonitorContinuation? ReadMonitorContinuation(OverlayMenuState state) {
        var invocation = ValidateMonitorInvocation(state);
        return invocation is { Bound: true } ? invocation.Frame : null;
    }

    private void MarkMonitorNormallyExpired(ulong revision) {
        if (monitorInvocation is { } invocation && invocation.Frame.Revision == revision)
            monitorInvocation = invocation with { Expired = true };
    }

    private void CancelMonitorContinuation() => monitorInvocation = null;
}
