using Apparition.Backend.Ipc;

namespace Apparition.Backend.Controller;

// This clock belongs to one admitted monitor branch, independent of callback and editor locks
internal sealed record LocalMenuMonitorWaitState(MenuMonitorWait Frame, object OwnerToken,
    ulong NavigationRevision, long StartedTimestamp, long LastTimestamp) {
    // The boxed immutable intent is owned only by the controller
    public object? ContinuationIntent { get; init; }
    public int DurationMilliseconds => Frame.Kind switch { "choice" => 180, "choiceNoop" => 50, _ => 250 };

    public LocalMenuMonitorWaitState? Read(TimeProvider clock) {
        long current = clock.GetTimestamp();
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(StartedTimestamp, current); }
        catch (OverflowException) { return Restart(current); }
        // Recovery retains the invocation and starts a full finite wait without replaying its action
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return Restart(current);
        return elapsed >= TimeSpan.FromMilliseconds(DurationMilliseconds) ? null : this with { LastTimestamp = current };
    }

    private LocalMenuMonitorWaitState Restart(long timestamp) =>
        this with { StartedTimestamp = timestamp, LastTimestamp = timestamp };
}
