using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Players;
using syndishanx_iw_tool.Players.LocalObservation;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // The copied intent binds logical rows and private targets, never changing display samples
    private sealed record UseIntent(ulong Sequence, object Owner, ulong AdmissionRevision,
        MenuSurface Surface, MenuPage Page, ulong NavigationRevision, int SelectedIndex,
        IReadOnlyList<MenuRow> Rows, PlayerIdentity? PersonalIdentity, Func<bool> ForegroundAllowed);
    private sealed record UseInvocation(UseIntent Intent, MenuUseContinuation Frame, bool Ready);
    private UseInvocation? useInvocation;
    private UseIntent? queuedUseIntent;

    private UseIntent? CaptureUseIntent(CommandEnvelope command, Func<bool> foregroundAllowed, OverlayMenuState state) {
        var proof = session.ReadLocalMenuAdmission();
        if (command.Input != OverlayInput.Select || command.Sequence == 0 || state.Preview || !state.Connected
            || !OrdinaryMonitorVisit || cleanupRecovery || ExitRequested || finished || !navigation.IsOpen
            || proof is null || admittedMenuOwner is null || !ReferenceEquals(admittedMenuOwner, proof.OwnerToken)
            || proof.Observation.ProcessId != processId || proof.Observation.HasMenu != true
            || proof.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)
            || navigation.SelectedIndex < 0 || navigation.SelectedIndex >= navigation.Rows.Count
            || navigation.Rows[navigation.SelectedIndex].IsSpacer) return null;
        // Immutable actions retain full player and cohort expectations without serializing them
        var rows = Array.AsReadOnly(navigation.Rows.ToArray());
        var action = rows[navigation.SelectedIndex].Action;
        var personal = action.Kind is MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
            ? state.PersonalGodmodeIdentity : null;
        return new(command.Sequence, proof.OwnerToken, proof.Revision, navigation.Surface, navigation.Page,
            navigation.Revision, navigation.SelectedIndex, rows, personal, foregroundAllowed);
    }

    private void CommitUseInvocation(UseIntent? intent, LocalMenuOptionCallbackState callback) {
        // A new invocation replaces prior ready history even when its action and color are equal
        useInvocation = intent is null ? null : new(intent,
            new(callback.Revision, intent.Sequence, intent.AdmissionRevision, callback.Kind), false);
        optionCallbackRevision = callback.Revision;
    }

    private bool OwnsDesignUseWait(UseInvocation invocation) =>
        designTransition is { CallerRevision: { } revision } && revision == invocation.Frame.Revision
        && !invocation.Ready && invocation.Frame.Kind == "plain" && invocation.Intent.Page == MenuPage.Preferences
        && invocation.Intent.Rows[invocation.Intent.SelectedIndex].Action is {
            Kind: MenuActionKind.ChangePreference, Slot: (int)MenuPreference.Design };

    private MenuOptionCallback? ReadUseCallbackFrame(MenuUseContinuation? receipt) {
        if (optionCallbackFrame is not null) return optionCallbackFrame;
        // Design owns the clock; this view keeps native input paused under the same caller identity
        return designTransition is { CallerRevision: { } revision }
            && receipt is { Kind: "plain" } && receipt.Revision == revision
            ? new(revision, "plain", "cooldown") : null;
    }

    private void MarkUseInvocationReady(ulong revision) {
        // Only the normal expiry of this exact callback can authorize held-input continuation
        if (useInvocation is { } invocation && invocation.Frame.Revision == revision) {
            useInvocation = invocation with { Ready = true };
        }
    }

    private MenuUseContinuation? ReadUseContinuation(OverlayMenuState state) {
        if (useInvocation is not { } invocation) return null;
        var intent = invocation.Intent;
        bool designWait = OwnsDesignUseWait(invocation);
        var proof = session.ReadLocalMenuAdmission();
        bool personal = intent.Rows[intent.SelectedIndex].Action.Kind is
            MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode;
        if (!navigation.IsOpen || finished || ExitRequested || cleanupRecovery || state.Preview || !state.Connected
            || !OrdinaryMonitorVisit || navigation.Surface != intent.Surface || navigation.Page != intent.Page
            || navigation.Revision != intent.NavigationRevision || navigation.SelectedIndex != intent.SelectedIndex
            || !navigation.RowsMatchTopology(intent.Rows) || !intent.ForegroundAllowed()
            || personal && state.PersonalGodmodeIdentity != intent.PersonalIdentity
            || proof is null || !ReferenceEquals(intent.Owner, admittedMenuOwner)
            || !ReferenceEquals(intent.Owner, proof.OwnerToken) || proof.Revision != intent.AdmissionRevision
            || proof.Observation.ProcessId != processId || proof.Observation.HasMenu != true
            || proof.Observation.Life is not (LocalPlayerLife.Alive or LocalPlayerLife.Dead)
            || monitorWait is not null || menuCredits is not null || widthEditor is not null && navigation.Page == MenuPage.WidthEditor
            || instructionPositionEditor is not null || menuPositionEditor is not null || menuUnlock.StartedTimestamp is not null
            || preferenceRefresh is not null || preferenceRebuild is not null || designTransition is not null && !designWait || openTransition is not null
            || !invocation.Ready && !designWait && (optionCallback is null || optionCallback.Revision != invocation.Frame.Revision)) {
            CancelUseContinuation();
            return null;
        }
        // Busy and disabled values remain truthful while the same owned action finishes
        return invocation.Frame;
    }

    private void CancelUseContinuation() => useInvocation = null;
}
