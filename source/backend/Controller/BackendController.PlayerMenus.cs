using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // Main retains its selected actor while Quick always uses the authenticated self route
    private PlayerMenuTarget? mainPlayerTarget;
    private PlayerMenuTarget? selectionCapturedTarget;
    private bool selectionReceiptRejected;

    private void ObservePlayerSelectionReceipt() {
        var action = selectionAfterCompletion;
        if (action?.SelectionScope is not { } scope || selectionReceiptRejected) return;
        var offered = session.ReadSelectedPlayerTarget();
        if (selectionCapturedTarget is not null) {
            // Once offered, a receipt cannot disappear or change before its task finishes
            if (!selectionCapturedTarget.Equals(offered)) selectionReceiptRejected = true;
            return;
        }
        if (scope.Revision != ulong.MaxValue && offered is not null
            && ReferenceEquals(scope.OwnerToken, offered.OwnerToken)
            && offered.SelectionRevision == scope.Revision + 1
            && offered.Identity.Slot == action.Slot
            && StringComparer.Ordinal.Equals(offered.Identity.Name, action.ExpectedName)) selectionCapturedTarget = offered;
    }

    private OverlayMenuState BindPlayerMenuState(OverlayMenuState state) {
        if (navigation.Page is not (MenuPage.SelectedPlayer or MenuPage.SelectedBasicScripts)) return state;
        var target = state.SelectedPlayerTarget;
        bool current = navigation.Surface == MenuSurface.Main && mainPlayerTarget is not null
            && mainPlayerTarget.Equals(target) && mainPlayerTarget.Equals(session.ReadSelectedPlayerTarget());
        return current ? state : state with { SelectedPlayerTarget = null };
    }

    private bool CompletePlayerSelection(MenuAction action, OverlayMenuState state) {
        var scope = action.SelectionScope;
        var target = state.SelectedPlayerTarget;
        // A rejected task can finish successfully without publishing the requested next selection
        if (selectionReceiptRejected || selectionCapturedTarget is null || !selectionCapturedTarget.Equals(target)
            || scope is null || target is null || scope.Revision == ulong.MaxValue
            || !ReferenceEquals(scope.OwnerToken, target.OwnerToken)
            || target.SelectionRevision != scope.Revision + 1
            || !target.Equals(session.ReadSelectedPlayerTarget())
            || target.Identity.Slot != action.Slot
            || !StringComparer.Ordinal.Equals(target.Identity.Name, action.ExpectedName)) return false;
        try {
            // Counter checks precede replacing the saved Main target
            navigation.OpenPage(MenuPage.SelectedPlayer);
            mainPlayerTarget = target;
            return true;
        } catch (OverflowException) {
            error = "The menu navigation counter is exhausted";
            return false;
        }
    }

    private bool HasCurrentPlayerTarget(MenuAction action) => action.ExpectedTarget is not null
        && mainPlayerTarget is not null && action.ExpectedTarget.Equals(mainPlayerTarget)
        && action.ExpectedTarget.Equals(session.ReadSelectedPlayerTarget());

    private bool AllowsPlayerMenuAction(MenuAction action, Func<bool> foregroundAllowed) {
        // Focus can change after the row was drawn, so validate it before reading current scope
        if (!foregroundAllowed()) return false;
        var state = BindPlayerMenuState(session.ReadMenuState());
        if (navigation.Surface != MenuSurface.Main || action.ExpectedTarget is null
            || !action.ExpectedTarget.Equals(state.SelectedPlayerTarget)
            || !action.ExpectedTarget.Equals(mainPlayerTarget)
            || !action.ExpectedTarget.Equals(session.ReadSelectedPlayerTarget())
            || state.Preview || !state.Connected || state.Busy) return false;
        if (action.Page == MenuPage.SelectedBasicScripts)
            return navigation.Page == MenuPage.SelectedPlayer;
        return action.Page == MenuPage.SelectedPlayer && navigation.Page == MenuPage.Players
            && action.ExpectedTarget.Route == PlayerMenuRoute.Teammate && state.CanRecoverSelectedTeammate;
    }
}
