namespace Apparition.Backend.Controller;

// SoftUnlock uses elapsed monotonic time while the owner loop keeps running
internal sealed record LocalMenuUnlockState(long? StartedTimestamp = null, long? LastTimestamp = null, bool ChromeRecreated = false,
    bool ChromeCreationReady = false, bool PauseExpired = false) {
    private static readonly TimeSpan RebuildDuration = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan TotalDuration = TimeSpan.FromMilliseconds(150);
    public static LocalMenuUnlockState None { get; } = new();

    public static LocalMenuUnlockState Start(TimeProvider clock) {
        long timestamp = clock.GetTimestamp();
        return new(timestamp, timestamp);
    }

    public (LocalMenuUnlockState State, string? Phase, bool RecreateChrome, bool ClockRecovered) Read(TimeProvider clock) {
        if (StartedTimestamp is not long started) return (this, null, false, false);
        long current = clock.GetTimestamp();
        // A faulty injected clock restarts the brief pause rather than unlocking early
        if (LastTimestamp is long previous && current < previous) return (new(current, current, ChromeRecreated), "rebuild", false, true);
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(started, current); }
        catch (OverflowException) { return (new(current, current, ChromeRecreated), "rebuild", false, true); }
        // A signed timestamp delta can overflow even when the new counter value is larger
        if (elapsed < TimeSpan.Zero) return (new(current, current, ChromeRecreated), "rebuild", false, true);
        // A late first read still requests the one createMenuHud that the source performed
        bool recreate = !ChromeRecreated && elapsed >= RebuildDuration;
        // A cheap phase read cannot discard the clock before the actual rows commit
        if (elapsed >= TotalDuration && ChromeRecreated) return (None, null, false, false);
        return (this with { LastTimestamp = current, ChromeCreationReady = recreate, PauseExpired = elapsed >= TotalDuration },
            elapsed >= TotalDuration ? null : elapsed < RebuildDuration ? "rebuild" : "pause", recreate, false);
    }

    // Legacy views also finish the redraw without requiring a published chrome identity
    public LocalMenuUnlockState CommitChromeCreation() => !ChromeCreationReady ? this
        : PauseExpired ? None : this with { ChromeRecreated = true, ChromeCreationReady = false };
}
