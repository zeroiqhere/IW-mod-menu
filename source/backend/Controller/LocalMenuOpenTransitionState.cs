namespace Apparition.Backend.Controller;

// Redraw readiness does not create a HUD until a fresh snapshot commits its actual rows
internal sealed record LocalMenuOpenTransitionState(ulong OpeningRevision, long StartedTimestamp,
    long LastTimestamp, bool Drawn = false) {
    public LocalMenuOpenTransitionState Draw(long timestamp) => this with {
        StartedTimestamp = timestamp, LastTimestamp = timestamp, Drawn = true
    };

    public (LocalMenuOpenTransitionState? State, string? Phase, bool ReadyToDraw, bool ClockRecovered) Read(TimeProvider clock, long current) {
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(StartedTimestamp, current); }
        catch (OverflowException) { return Restart(current); }
        // Recovery keeps an existing HUD visible and never starts a second fade
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return Restart(current);
        if (Drawn && elapsed >= TimeSpan.FromMilliseconds(550)) return (null, null, false, false);
        return (this with { LastTimestamp = current }, Drawn ? "cooldown" : "rebuild",
            !Drawn && elapsed >= TimeSpan.FromMilliseconds(50), false);
    }

    private (LocalMenuOpenTransitionState State, string Phase, bool ReadyToDraw, bool ClockRecovered) Restart(long timestamp) =>
        (this with { StartedTimestamp = timestamp, LastTimestamp = timestamp }, Drawn ? "cooldown" : "rebuild", false, true);
}
