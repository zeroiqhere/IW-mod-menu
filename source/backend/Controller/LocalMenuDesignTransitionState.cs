using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// Design applies immediately while the old footer survives the first fifty milliseconds
// The optional caller revision links this timer to one admitted Use without another clock
internal sealed record LocalMenuDesignTransitionState(string PreviousDesign, bool Changed, long StartedTimestamp,
    long LastTimestamp, int BaseMilliseconds = 0, bool PresentationApplied = false, ulong? CallerRevision = null)
{
    public static LocalMenuDesignTransitionState Start(MenuAppearance previous, bool changed, TimeProvider clock)
    {
        long timestamp = clock.GetTimestamp();
        return new(previous.Design, changed, timestamp, timestamp);
    }

    public (LocalMenuDesignTransitionState? State, string? Phase, bool RefreshPresentation, bool ClockRecovered) Read(TimeProvider clock) => Read(clock, clock.GetTimestamp());

    public (LocalMenuDesignTransitionState? State, string? Phase, bool RefreshPresentation, bool ClockRecovered) Read(TimeProvider clock, long current)
    {
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(StartedTimestamp, current); }
        catch (OverflowException) { return Restart(current); }
        // Clock recovery cannot undo a completed rebuild or request a second redraw
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return Restart(current);
        bool refresh = Changed && !PresentationApplied && elapsed >= TimeSpan.FromMilliseconds(50 - BaseMilliseconds);
        if (elapsed >= TimeSpan.FromMilliseconds(250 - BaseMilliseconds)) return (null, null, refresh, false);
        return (this with { LastTimestamp = current }, Changed && !PresentationApplied && !refresh ? "rebuild" : "cooldown", refresh, false);
    }

    private (LocalMenuDesignTransitionState State, string Phase, bool RefreshPresentation, bool ClockRecovered) Restart(long timestamp) =>
        (this with { StartedTimestamp = timestamp, LastTimestamp = timestamp, BaseMilliseconds = PresentationApplied ? 50 : 0 },
            Changed && !PresentationApplied ? "rebuild" : "cooldown", false, true);

    // Only the design selector changes the footer layout during the temporary old frame
    public MenuAppearance DisplayAppearance(MenuAppearance actual) => Changed && !PresentationApplied
        ? actual with { Design = PreviousDesign } : actual;
}
