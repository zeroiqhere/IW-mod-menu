using Apparition.Backend.Ipc;
using Apparition.Backend.OpenControls;
using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    private ulong optionCallbackRevision;
    private LocalMenuOptionCallbackState? optionCallback;
    private MenuOptionCallback? optionCallbackFrame;
    private MenuSurface optionCallbackSurface;
    private MenuPage optionCallbackPage;
    private ulong optionCallbackNavigation;
    private object? optionCallbackOwner;
    // The source HUD retains dependent preference fields until this callback's redraw
    private MenuAppearance? optionCallbackAppearance;
    // Only the old display policy is retained; opening admission always reads the applied policy
    private QuickOpeningPolicies? optionCallbackOpeningPolicies;
    private MainOpeningChord? optionCallbackMainOpeningChord;

    private void BeginOptionCallback(MenuAction action, UseIntent? intent) {
        // Legacy packet producers retain their existing contract without claiming a source actor
        if (!session.RequiresLocalMenuAdmission && session.ReadLocalMenuAdmission() is null) return;
        string? kind = action.Kind switch {
            MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
                or MenuActionKind.ToggleTeammateGodmode or MenuActionKind.ToggleAllPlayersGodmode => "boolean",
            // IW connection and roster work has no direct BO3 gameplay callback equivalent
            // The current binary lobby-ammo adapter uses the ordinary slider caller wait
            MenuActionKind.Connect or MenuActionKind.RefreshPlayers or MenuActionKind.SelectPlayer
                or MenuActionKind.ToggleLobbyAmmo => "plain",
            _ => null
        };
        if (kind is null || !navigation.IsOpen || cleanupRecovery || ExitRequested) { CancelUseContinuation(); return; }
        CommitOptionCallback(PrepareOptionCallback(kind), intent);
    }

    private LocalMenuOptionCallbackState PrepareOptionCallback(string kind) {
        // Reserve the epoch and clock before starting any session work or local mutation
        ulong revision = checked(optionCallbackRevision + 1);
        if (kind == "boolean") {
            _ = checked(navigation.MenuRevision + 1);
            // These local settings retain their own rows without publishing the indicator cache
            if (navigation.Page is not (MenuPage.Preferences or MenuPage.OpenControls)) PreflightBooleanPaintRedraw();
        }
        return LocalMenuOptionCallbackState.Start(revision, kind, clock);
    }

    private void CommitOptionCallback(LocalMenuOptionCallbackState? prepared, UseIntent? intent = null,
        MenuAppearance? previousAppearance = null, QuickOpeningPolicies? previousOpeningPolicies = null,
        MainOpeningChord? previousMainOpeningChord = null) {
        if (prepared is null) return;
        optionCallbackSurface = navigation.Surface;
        optionCallbackPage = navigation.Page;
        optionCallbackNavigation = navigation.Revision;
        optionCallbackOwner = admittedMenuOwner;
        optionCallbackAppearance = previousAppearance;
        optionCallbackOpeningPolicies = previousOpeningPolicies;
        // The pair already owns its Main chord, so the fallback is stored only without a pair
        optionCallbackMainOpeningChord = previousOpeningPolicies is null ? previousMainOpeningChord : null;
        CommitUseInvocation(intent, prepared);
        optionCallback = prepared;
        if (navigation.Surface == MenuSurface.Quick) quickWaitReplacedByCallback = true;
        optionCallbackFrame = new(prepared.Revision, prepared.Kind, prepared.Kind == "boolean" ? "pending" : "cooldown");
    }

    private bool OwnsOptionCallback() {
        if (!navigation.IsOpen || ExitRequested || cleanupRecovery
            || navigation.Surface != optionCallbackSurface || navigation.Page != optionCallbackPage
            || navigation.Revision != optionCallbackNavigation) return false;
        // A disconnected administrative Connect visit owns no actor to borrow
        if (optionCallbackOwner is null) return admittedMenuOwner is null;
        var current = session.ReadLocalMenuAdmission();
        return ReferenceEquals(optionCallbackOwner, admittedMenuOwner)
            && current is not null && ReferenceEquals(optionCallbackOwner, current.OwnerToken)
            && current.Observation.ProcessId == processId && current.Observation.HasMenu == true;
    }

    private void AdvanceOptionCallback() {
        if (optionCallback is null) return;
        if (!OwnsOptionCallback()) { CancelOptionCallback(); return; }
        var next = optionCallback.Read(clock);
        if (next.RefreshPresentation) {
            // The clock's own monitor lock cannot veto its scheduled redraw
            // Counter admission precedes retiring opacity or committing the clock sample
            navigation.InvalidatePresentation();
            CancelOpening();
        }
        if (next.State is null) MarkUseInvocationReady(optionCallback.Revision);
        optionCallback = next.State is { } pending && next.RefreshPresentation
            ? pending with { PresentationApplied = true } : next.State;
        optionCallbackFrame = next.Frame;
        // Only a successful redraw or normal expiry retires the retained style
        if (next.RefreshPresentation || next.State is null) {
            optionCallbackAppearance = null;
            optionCallbackOpeningPolicies = null;
            optionCallbackMainOpeningChord = null;
        }
    }

    private MenuAppearance CallbackRowsAppearance(MenuAppearance actual) =>
        optionCallback is { PresentationApplied: false } && optionCallbackAppearance is { } previous
            ? LocalMenuPreferenceRefreshState.RetainAppearance(actual, previous) : actual;

    private void RebindPlainCallbackAfterSelection() {
        // Only the verified result of this owned player-selection callback may change its page
        if (optionCallback?.Kind != "plain") return;
        optionCallbackPage = navigation.Page;
        optionCallbackNavigation = navigation.Revision;
    }

    private void CancelOptionCallback() {
        CancelUseContinuation();
        optionCallback = null;
        optionCallbackFrame = null;
        optionCallbackOwner = null;
        optionCallbackAppearance = null;
        optionCallbackOpeningPolicies = null;
        optionCallbackMainOpeningChord = null;
    }
}
