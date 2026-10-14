using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Counter handlers replace the panel before the chosen local input cooldown ends
internal sealed record LocalMenuPreferenceRebuildState(MenuAppearance PreviousAppearance, MenuPreferenceRebuildCause Cause,
    long StartedTimestamp, long LastTimestamp, int BaseMilliseconds = 0, bool PresentationApplied = false) {
    public static LocalMenuPreferenceRebuildState Start(MenuAppearance previous, MenuPreferenceRebuildCause cause, TimeProvider clock) {
        if (!Enum.IsDefined(cause)) throw new ArgumentOutOfRangeException(nameof(cause));
        long timestamp = clock.GetTimestamp();
        return new(previous, cause, timestamp, timestamp);
    }

    public (LocalMenuPreferenceRebuildState? State, string? Phase, bool RefreshPresentation, bool ClockRecovered)
        Read(TimeProvider clock, long current) {
        TimeSpan elapsed;
        try {
            elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        } catch (OverflowException) {
            return Restart(current);
        }
        // Recovery keeps an applied panel and restarts only its remaining stage
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return Restart(current);
        bool refresh = !PresentationApplied && elapsed >= TimeSpan.FromMilliseconds(50 - BaseMilliseconds);
        if (elapsed >= TimeSpan.FromMilliseconds(250 - BaseMilliseconds)) return (null, null, refresh, false);
        string phase = !PresentationApplied && !refresh ? "rebuild" : "cooldown";
        return (this with { LastTimestamp = current }, phase, refresh, false);
    }

    private (LocalMenuPreferenceRebuildState State, string Phase, bool RefreshPresentation, bool ClockRecovered)
        Restart(long timestamp) => (this with { StartedTimestamp = timestamp, LastTimestamp = timestamp,
            BaseMilliseconds = PresentationApplied ? 50 : 0 }, PresentationApplied ? "cooldown" : "rebuild", false, true);

    // The captured appearance keeps Stealth, display mode, and counter mutually consistent
    public MenuAppearance DisplayAppearance(MenuAppearance actual) => PresentationApplied ? actual : PreviousAppearance;
}
