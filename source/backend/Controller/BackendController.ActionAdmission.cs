using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

public sealed partial class BackendController {
    // Keep private source authority with the accepted intent rather than a later display frame
    private sealed record QueuedActionScope(MenuSurface Surface, MenuPage Page, ulong NavigationRevision,
        object? Owner, ulong? LocalRevision, Func<bool> ForegroundAllowed);
    private QueuedActionScope? queuedActionScope;

    private QueuedActionScope? CaptureQueuedActionScope(Func<bool> foregroundAllowed) {
        var admission = session.ReadLocalMenuAdmission();
        // Older session adapters have no actor admission contract to impersonate
        if (!session.RequiresLocalMenuAdmission && admission is null) return null;
        return new(navigation.Surface, navigation.Page, navigation.Revision,
            admittedMenuOwner, admission?.Revision, foregroundAllowed);
    }

    private bool AllowsQueuedActionScope(QueuedActionScope? scope) {
        if (scope is null) return true;
        if (!navigation.IsOpen || cleanupRecovery || navigation.Surface != scope.Surface
            || navigation.Page != scope.Page || navigation.Revision != scope.NavigationRevision
            || !scope.ForegroundAllowed()) return false;
        var current = session.ReadLocalMenuAdmission();
        // A disconnected Connect bootstrap may keep its exact actorless scope
        if (scope.Owner is null) return admittedMenuOwner is null && scope.LocalRevision is null && current is null;
        return ReferenceEquals(scope.Owner, admittedMenuOwner) && current is not null
            && ReferenceEquals(scope.Owner, current.OwnerToken) && scope.LocalRevision == current.Revision
            && current.Observation.ProcessId == processId && current.Observation.HasMenu == true;
    }

    private void DropPendingActionState() {
        // Dropped accepted commands remain acknowledged and cannot become a deferred replay
        retainedState = null;
        selectionRevision = null;
        selectionForegroundAllowed = null;
        selectionCompletionScope = null;
        selectionCapturedTarget = null;
        selectionReceiptRejected = false;
    }
}
