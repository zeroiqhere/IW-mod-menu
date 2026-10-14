using syndishanx_iw_tool.Features.Godmode;
using syndishanx_iw_tool.Overlay;
using syndishanx_iw_tool.Overlay.Players;
using syndishanx_iw_tool.Players;

namespace Apparition.Backend.Session;

internal sealed partial class RetailBackendSession {
    // Metadata, identity and generation publish together rather than as separate slot/name fields
    private sealed record PlayerSelectionContext(RetailConnection Owner, object Token,
        ulong Revision, DetectedPlayer? Player = null, PlayerMenuTarget? Target = null);
    private PlayerSelectionContext? playerSelection;
    private readonly SemaphoreSlim targetGate = new(1, 1);

    // A cached scope is admission metadata, not proof that its connection is still live
    public PlayerMenuSelectionScope? ReadPlayerSelectionScope() {
        var context = Volatile.Read(ref playerSelection);
        return context is not null && CanCommit(context.Owner) && context.Owner.HasConfirmedConnection
            && ReferenceEquals(context, Volatile.Read(ref playerSelection))
            ? new(context.Token, context.Revision) : null;
    }

    // This fresh projection confirms a receipt, but never replaces an action's captured expectation
    public PlayerMenuTarget? ReadSelectedPlayerTarget() => ReadMenuState().SelectedPlayerTarget;

    private MenuAction CapturePlayerAction(MenuAction action) {
        ArgumentNullException.ThrowIfNull(action);
        var context = Volatile.Read(ref playerSelection);
        // Entry capture performs no transport reads and cannot wait behind a native proof query
        if (action.Kind == MenuActionKind.SelectPlayer && action.SelectionScope is null) {
            return action with { SelectionScope = context is null ? null : new(context.Token, context.Revision) };
        }
        if (action.Page != MenuPage.SelectedBasicScripts && action.ExpectedTarget is null
            && (action.Kind == MenuActionKind.ToggleTeammateGodmode
                || action.Kind == MenuActionKind.TogglePersonalGodmode && action.Slot >= 0)) {
            return action with { ExpectedTarget = context?.Target };
        }
        return action;
    }

    private bool MatchesOriginalConnection(RetailConnection owner) => CanCommit(owner)
        && owner.ObserveOriginalConnection() && CanCommit(owner);

    private bool MatchesSelectionScope(RetailConnection owner, PlayerMenuSelectionScope? expected,
        out PlayerSelectionContext? context) {
        context = Volatile.Read(ref playerSelection);
        return expected is not null && context is not null && MatchesOriginalConnection(owner)
            && ReferenceEquals(context.Owner, owner) && ReferenceEquals(expected.OwnerToken, context.Token)
            && expected.Revision == context.Revision;
    }

    private bool MatchesTargetAction(RetailConnection owner, MenuAction action) {
        var context = Volatile.Read(ref playerSelection);
        var target = action.ExpectedTarget;
        if (target is null || context?.Target is null || !ReferenceEquals(context.Owner, owner)
            || !context.Target.Equals(target) || !CanCommit(owner)
            || action.Slot != target.Identity.Slot
            || !StringComparer.Ordinal.Equals(action.ExpectedName, target.Identity.Name)) return false;
        if (action.Page == MenuPage.SelectedBasicScripts) {
            bool correctRoute = target.Route == PlayerMenuRoute.Personal
                ? action.Kind is MenuActionKind.TogglePersonalGodmode or MenuActionKind.RestorePersonalGodmode
                : action.Kind == MenuActionKind.ToggleTeammateGodmode;
            if (!correctRoute) return false;
        }
        // Native service guards still decide ON versus retained OFF after this private scope check
        return target.Equals(ReadSelectedPlayerTarget())
            && ReferenceEquals(context, Volatile.Read(ref playerSelection));
    }

    private async Task ExecuteTargetActionAsync(RetailConnection owner, MenuAction action) {
        // This operation gate excludes selection changes while its original target service is awaited
        // The lifecycle gate remains available for shutdown and connection retirement
        await targetGate.WaitAsync();
        try {
            if (!MatchesOriginalConnection(owner) || !foregroundAllowed()) return;
            if (action.Kind == MenuActionKind.SelectPlayer) {
                await SelectPlayerAsync(owner, action);
            } else if (MatchesTargetAction(owner, action)) {
                switch (action.Kind) {
                    case MenuActionKind.ToggleTeammateGodmode: await ToggleTeammateAsync(owner, action); break;
                    case MenuActionKind.TogglePersonalGodmode: await ToggleGodmodeAsync(owner, action); break;
                    case MenuActionKind.RestorePersonalGodmode: await RestoreGodmodeAsync(owner); break;
                }
            }
        } finally { targetGate.Release(); }
    }

    private PlayerIdentity? AuthenticatedLocalIdentity(RetailConnection owner) {
        var admission = Volatile.Read(ref localMenuAdmission);
        return admission is not null && ReferenceEquals(admission.Owner, owner) && CanCommit(owner)
            ? admission.Receipt.Binding.Identity : null;
    }

    private async Task SelectPlayerAsync(RetailConnection owner, MenuAction action) {
        if (!MatchesSelectionScope(owner, action.SelectionScope, out var context)
            || context!.Revision == ulong.MaxValue) return;
        // Reserve the next generation before strict capture can retain a new feature adapter
        ulong nextRevision = checked(context.Revision + 1);
        var selected = Volatile.Read(ref players).SingleOrDefault(player => player.Slot == action.Slot
            && StringComparer.Ordinal.Equals(player.Name, action.ExpectedName));
        if (selected is null || owner.Teammates.ReadView().HasPendingCleanup) return;
        var local = owner.LocalGodmode;
        var personal = local?.ReadView();
        var boundSelf = personal?.HasBoundTarget == true ? personal.Identity : null;
        var authenticatedSelf = AuthenticatedLocalIdentity(owner);
        var self = authenticatedSelf ?? boundSelf;
        if (self is not null && self.Slot == selected.Slot) {
            // A replacement local binding cannot borrow an older personal writer or become a teammate
            if (boundSelf is not null && authenticatedSelf is not null && boundSelf != authenticatedSelf
                || !StringComparer.Ordinal.Equals(self.Name, selected.Name)) return;
            if (local is not null) await local.CaptureAsync();
            var current = local?.ReadView();
            bool matchesPersonal = current?.HasBoundTarget == true && current.Identity == self;
            bool matchesAccount = AuthenticatedLocalIdentity(owner) == self;
            if (!matchesPersonal && !matchesAccount) return;
            // Verified self metadata can open Options while its personal feature remains unavailable
            await CommitSelectionAsync(owner, context, selected,
                new(self, PlayerMenuRoute.Personal, context.Token, nextRevision));
            return;
        }
        bool succeeded = await owner.Teammates.SelectAsync(selected.Slot, selected.Name);
        var view = owner.Teammates.ReadView();
        if (!succeeded || !view.HasCurrentIdentity || view.Identity is not { } identity || identity.Slot != selected.Slot
            || !StringComparer.Ordinal.Equals(identity.Name, selected.Name)) return;
        await CommitSelectionAsync(owner, context, selected,
            new(identity, PlayerMenuRoute.Teammate, context.Token, nextRevision));
    }

    private async Task CommitSelectionAsync(RetailConnection owner, PlayerSelectionContext expected,
        DetectedPlayer selected, PlayerMenuTarget target) {
        await CommitAsync(owner, () => {
            if (!ReferenceEquals(expected, Volatile.Read(ref playerSelection))
                || !MatchesOriginalConnection(owner)) return;
            if (target.Route == PlayerMenuRoute.Personal) {
                var personal = owner.LocalGodmode?.ReadView();
                var bound = personal?.HasBoundTarget == true ? personal.Identity : null;
                var authenticated = AuthenticatedLocalIdentity(owner);
                if (bound is not null && bound != target.Identity
                    || authenticated is not null && authenticated != target.Identity
                    || bound != target.Identity && authenticated != target.Identity) return;
            }
            Volatile.Write(ref playerSelection, new(owner, expected.Token, target.SelectionRevision, selected, target));
            Log(target.Route == PlayerMenuRoute.Personal ? "Selected your verified local actor"
                : "The selected teammate state was captured without changing flags");
        });
    }

    private async Task ToggleTeammateAsync(RetailConnection owner, MenuAction action) {
        // The captured route never changes merely because a later local observation becomes available
        if (action.ExpectedTarget?.Route == PlayerMenuRoute.Personal) {
            await ToggleGodmodeAsync(owner, action);
            return;
        }
        await owner.Teammates.ToggleAsync(action.Slot, action.ExpectedName!);
        string status = owner.Teammates.ReadView().Status;
        await CommitAsync(owner, () => Log("Teammate Godmode: " + status));
        await RefreshCohortGodmodeAsync(owner);
    }

    private void ClearMissingSelection(IReadOnlyList<DetectedPlayer> roster,
        SelectedGodmodeView? freshTeammateView = null) {
        var context = Volatile.Read(ref playerSelection);
        if (context?.Player is null) return;

        bool rosterContainsSelection = roster.Contains(context.Player);
        if (context.Target?.Route == PlayerMenuRoute.Teammate) {
            bool originalIdentityConfirmed = freshTeammateView?.HasCurrentIdentity == true
                && freshTeammateView.Identity == context.Target.Identity;
            if (originalIdentityConfirmed && (rosterContainsSelection
                || freshTeammateView!.CanToggle
                    && (freshTeammateView.Value == GodmodeValue.On || freshTeammateView.HasPendingCleanup))) return;
        } else if (rosterContainsSelection) {
            return;
        }

        // An absent candidate keeps only a freshly proven owner that still needs OFF cleanup
        // A failed proof or a later roster reappearance cannot revive the old selection receipt
        ulong revision = context.Revision == ulong.MaxValue ? ulong.MaxValue : checked(context.Revision + 1);
        Volatile.Write(ref playerSelection, context with { Revision = revision, Player = null, Target = null });
    }
}
