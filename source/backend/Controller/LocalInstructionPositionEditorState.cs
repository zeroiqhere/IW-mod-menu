namespace Apparition.Backend.Controller;

// Short source waits advance on the existing owner loop without blocking its heartbeat
internal sealed record LocalInstructionPositionEditorState(string Phase, long StartedTimestamp, long LastTimestamp) {
    private static readonly TimeSpan TransitionDuration = TimeSpan.FromMilliseconds(100);
    public static LocalInstructionPositionEditorState Enter(TimeProvider clock) => Start("entering", clock);
    public LocalInstructionPositionEditorState Exit(TimeProvider clock) => Start("exiting", clock);
    private static LocalInstructionPositionEditorState Start(string phase, TimeProvider clock) {
        long timestamp = clock.GetTimestamp();
        return new(phase, timestamp, timestamp);
    }
    public (LocalInstructionPositionEditorState? State, bool Finished) Read(TimeProvider clock) {
        long current = clock.GetTimestamp();
        if (Phase == "editing") return (this with { LastTimestamp = current }, false);
        TimeSpan elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        // A broken counter restarts only the current short wait and retains all position edits
        if (current < LastTimestamp || elapsed < TimeSpan.Zero) return (new(Phase, current, current), false);
        if (elapsed < TransitionDuration) return (this with { LastTimestamp = current }, false);
        return Phase == "entering" ? (new("editing", current, current), false) : (null, true);
    }
}
