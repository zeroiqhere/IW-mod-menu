using Apparition.Backend.Ipc;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private ulong booleanPaintRevision;
    private MenuBooleanPaintCache? booleanPaint;
    private object? booleanPaintOwner;
    private MenuSurface booleanPaintSurface;
    private MenuPage booleanPaintPage;
    private ulong booleanPaintMenuRevision;

    private bool CanPublishBooleanPaint(bool drawingOpen) {
        if (!navigation.IsOpen || ExitRequested || cleanupRecovery || admittedMenuOwner is null
            || widthEditor is not null || instructionPositionEditor is not null || menuPositionEditor is not null
            || menuCredits is not null || menuUnlock.StartedTimestamp is not null || preferenceRefresh is not null
            || designTransition is not null || preferenceRebuild is not null
            || openTransitionPhase == "rebuild" && !drawingOpen) return false;
        var current = session.ReadLocalMenuAdmission();
        // Only the admitted actor owns this visit; names and row values prove no authority
        return current is not null && ReferenceEquals(current.OwnerToken, admittedMenuOwner)
            && current.Observation.ProcessId == processId && current.Observation.HasMenu == true;
    }

    private MenuBooleanPaintCache? PrepareBooleanPaint(IReadOnlyList<MenuRow> rows, bool drawingOpen, bool gameplayView) {
        // Inert preview and disconnected frames keep their existing metadata contract
        if (!gameplayView || !CanPublishBooleanPaint(drawingOpen)) return null;
        if (booleanPaint is not null && ReferenceEquals(booleanPaintOwner, admittedMenuOwner)
            && booleanPaintSurface == navigation.Surface && booleanPaintPage == navigation.Page
            && booleanPaintMenuRevision == navigation.MenuRevision && booleanPaint.MatchesTopology(rows)) {
            // Cursor movement and worker completion keep the last source redraw's display sample
            return booleanPaint;
        }
        bool hasBoolean = rows.Any(row => !row.IsSpacer && row.Action.Kind is
            MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
            or MenuActionKind.ToggleTeammateGodmode or MenuActionKind.ToggleAllPlayersGodmode
            || navigation.Page == MenuPage.ThemeColors && row.Action.Kind is
                MenuActionKind.ChangeAccent or MenuActionKind.EnableSmoothRainbowTheme
            || navigation.Page == MenuPage.Instructions && IsInstructionBooleanAction(row.Action));
        if (!hasBoolean) return null;
        // Prepare the owned copy before navigation can commit its next structural epoch
        ulong revision = checked(booleanPaintRevision + 1);
        if (optionCallback is { Kind: "boolean", PresentationApplied: false }) {
            // A topology refresh before 180 ms must leave the scheduled paint epoch available
            _ = checked(revision + 1);
        }
        return MenuBooleanPaintCache.Capture(revision, rows, includePalette: navigation.Page == MenuPage.ThemeColors,
            includeInstructions: navigation.Page == MenuPage.Instructions);
    }

    private void PreflightPendingBooleanRows(IReadOnlyList<MenuRow> rows) {
        if (optionCallback is not { Kind: "boolean", PresentationApplied: false }
            || navigation.RowsMatchTopology(rows)) return;
        // Life changes can remove every boolean, but still consume a structural menu epoch
        // The early commit must leave both possible scheduled context commits available
        _ = checked(navigation.MenuRevision + 3);
    }

    private void CommitBooleanPaint(MenuBooleanPaintCache? prepared) {
        booleanPaint = prepared;
        booleanPaintOwner = prepared is null ? null : admittedMenuOwner;
        booleanPaintSurface = navigation.Surface;
        booleanPaintPage = navigation.Page;
        booleanPaintMenuRevision = navigation.MenuRevision;
        if (prepared is not null) booleanPaintRevision = prepared.Frame.Revision;
    }

    private void PreflightBooleanPaintRedraw() {
        // A callback cannot dispatch game work if its later paint epoch cannot be published
        _ = checked(booleanPaintRevision + 1);
        // Scheduled invalidation and a changed recovery action may each need one context epoch
        _ = checked(navigation.MenuRevision + 2);
    }
}
