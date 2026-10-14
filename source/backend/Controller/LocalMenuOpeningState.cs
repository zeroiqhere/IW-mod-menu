using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// An armed visit starts only when the owner actually replaces the option presentation
internal sealed record LocalMenuOpeningState(ulong Revision, int DurationMilliseconds, long? StartedTimestamp = null,
    long LastTimestamp = 0, ulong PresentationRevision = 0) {
    public LocalMenuOpeningState Activate(long timestamp, ulong presentationRevision) => this with {
        StartedTimestamp = timestamp, LastTimestamp = timestamp, PresentationRevision = presentationRevision
    };

    public (LocalMenuOpeningState? State, MenuOpening? Frame) Read(TimeProvider clock, long current) {
        if (StartedTimestamp is not long started) return (this, null);
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(started, current); }
        catch (OverflowException) { return (null, null); }
        // Broken clocks complete opacity safely rather than redisplaying invisible rows
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue
            || elapsed >= TimeSpan.FromMilliseconds(DurationMilliseconds)) return (null, null);
        return (this with { LastTimestamp = current }, new(Revision, (int)elapsed.TotalMilliseconds));
    }
}
