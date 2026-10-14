using Apparition.Backend.Ipc;
using Apparition.Backend.Controller;

namespace Apparition.Backend.Runtime;

// Track the committed owner phase separately from a focus-hidden display frame
internal sealed class MenuPhasePublication {
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromMilliseconds(100);
    private readonly Action<StateDocument> writeState;
    private readonly TimeProvider clock;
    private string? lastPublishedOwnerPhase;
    private bool lastPublishedFocus;
    private long? lastPublishedTimestamp;
    private long? lastObservedTimestamp;
    private bool rollbackPending;

    public MenuPhasePublication(Action<StateDocument> writeState, TimeProvider? clock = null) {
        this.writeState = writeState ?? throw new ArgumentNullException(nameof(writeState));
        this.clock = clock ?? TimeProvider.System;
    }

    public bool HasChanged(string? ownerPhase) => !StringComparer.Ordinal.Equals(ownerPhase, lastPublishedOwnerPhase);
    public bool ShouldPublish(string? ownerPhase, bool foregroundAllowed) {
        long current = clock.GetTimestamp();
        // A rollback remains due until a fresh frame actually commits
        if (lastObservedTimestamp is long previous && current < previous) rollbackPending = true;
        lastObservedTimestamp = current;
        if (lastPublishedTimestamp is not long committed) return true;
        TimeSpan elapsed = clock.GetElapsedTime(committed, current);
        if (elapsed < TimeSpan.Zero) rollbackPending = true;
        return rollbackPending || elapsed >= HeartbeatInterval || foregroundAllowed != lastPublishedFocus || HasChanged(ownerPhase);
    }
    public void Publish(StateDocument ownerState, bool foregroundAllowed) {
        writeState(StateFocusProjection.Apply(ownerState, foregroundAllowed));
        // Start the next interval after the successful write has finished
        long committed = clock.GetTimestamp();
        string? primary = ownerState.MenuOpenTransition is { } open ? "open:" + open.Phase
            : ownerState.MenuDesignTransition is { } design ? "design:" + design.Phase
            : ownerState.MenuPreferenceRebuild is { } rebuild ? "preference-rebuild:" + rebuild.Phase
            : ownerState.MenuPreferenceRefresh is { } preference ? "preference:" + preference.Phase
            : ownerState.MenuOptionCallback is { } callback ? MenuPublicationPhase.Callback(callback)
            : ownerState.MenuCredits is not null ? "credits"
            : ownerState.InstructionPositionEditor is { } editor ? "position:" + editor.Phase
            : ownerState.MenuUnlock;
        lastPublishedOwnerPhase = MenuPublicationPhase.ComposeMechanical(MenuPublicationPhase.ComposeUse(MenuPublicationPhase.ComposeMonitor(
            MenuPublicationPhase.Compose(primary, ownerState.MenuOpening), ownerState.MenuMonitorWait), ownerState.MenuUseContinuation), ownerState.MenuMonitorContinuation);
        lastPublishedFocus = foregroundAllowed;
        lastPublishedTimestamp = committed;
        lastObservedTimestamp = committed;
        rollbackPending = false;
    }
}
