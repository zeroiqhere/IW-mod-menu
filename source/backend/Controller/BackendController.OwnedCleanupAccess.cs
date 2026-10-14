using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private const string OwnedCleanupGuidance = "Player information is unavailable. Select Exit to attempt restoring this menu's changes for every affected player and stop the menu";

    private MenuSnapshot BuildOwnedCleanupMenu() => new("Session cleanup", "CLEANUP",
        Array.AsReadOnly(new[] { new MenuRow("Exit", "", true, new(MenuActionKind.Exit), OwnedCleanupGuidance) }),
        OwnedCleanupGuidance);

    private bool OwnsCleanupVisit(OwnedCleanupVisit visit) => !finished && !ExitRequested && !Busy
        && !session.ReadMenuState().Busy && session.ReadLocalMenuAdmission() is null
        && ReferenceEquals(visit.Owner, session.ReadOwnedCleanupOwner());

    private void ObserveOwnedCleanupVisit() {
        if (cleanupVisit is OwnedCleanupVisit visit) {
            if (OwnsCleanupVisit(visit)) return;
            // Returning actor proof or a retired writer closes this visit without any feature write
            navigation.Close();
            cleanupVisit = null;
            CancelPreferenceRefresh();
        }
        if (cleanupVisit is FailedCleanupVisit || finished || ExitRequested) return;
        object? owner = ReadCleanupOpeningOwner();
        object? previous = (cleanupVisit as OfferedCleanupVisit)?.Owner;
        if (ReferenceEquals(owner, previous)) return;
        // The existing navigation epoch binds the displayed opening to this original connection
        navigation.InvalidateActionContext();
        cleanupVisit = owner is null ? null : new OfferedCleanupVisit(owner);
    }

    private object? ReadCleanupOpeningOwner() {
        if (navigation.IsOpen || Busy || !session.RequiresLocalMenuAdmission || CurrentMainOpeningChord is null
            || session.ReadLocalMenuAdmission() is not null) return null;
        var state = session.ReadMenuState();
        return state.Connected && !state.Preview && !state.Busy ? session.ReadOwnedCleanupOwner() : null;
    }

    private bool HandleOwnedCleanupInput(CommandEnvelope command, Func<bool> foregroundAllowed) {
        if (cleanupVisit is OwnedCleanupVisit visit) {
            // This private owner cannot become a normal menu, Quick visit, or dedicated editor
            if (!navigation.IsOpen || !OwnsCleanupVisit(visit) || !foregroundAllowed()
                || !AllowsCapturedOpeningScope(command)
                || command.NavigationRevision is ulong navigationRevision && navigationRevision != navigation.Revision
                || command.MenuRevision is not null || command.LocalAdmissionRevision is not null) return true;
            if (command.Input is OverlayInput.Back or OverlayInput.Toggle
                || command.Input == OverlayInput.Close && appearance.QuickExit && command.NavigationRevision is not null) {
                navigation.Close();
                cleanupVisit = null;
                CancelPreferenceRefresh();
            } else if (command.Input == OverlayInput.Select) {
                // Shutdown performs the existing guarded restoration and retains failed owners for retry
                RequestShutdown();
            }
            return true;
        }
        if (command.Input != OverlayInput.Toggle || navigation.IsOpen || cleanupRecovery
            || !session.RequiresLocalMenuAdmission || session.ReadLocalMenuAdmission() is not null) return false;
        var state = session.ReadMenuState();
        // Disconnected bootstrap navigation keeps its existing Connect path
        if (!state.Connected) return false;
        var offer = cleanupVisit as OfferedCleanupVisit;
        if (state.Preview || state.Busy || Busy || offer is null
            || command.LocalAdmissionRevision is not null || command.MenuRevision is not null
            || command.NavigationRevision != navigation.Revision
            || command.OpeningControlsRevision is null || command.OpeningControlsRevision != OpeningAdmissionRevision
            || !ReferenceEquals(offer.Owner, ReadCleanupOpeningOwner())
            || !foregroundAllowed()) return true;
        // Counter admission comes before retiring callbacks or capturing the original cleanup owner
        navigation.OpenCleanupRoot();
        CancelPreferenceRefresh();
        admittedMenuOwner = null;
        cleanupVisit = new OwnedCleanupVisit(offer.Owner);
        return true;
    }
}
