using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// The existing owner loop advances credits without starting a worker or blocking input
internal sealed record LocalMenuCreditsState(long StartedTimestamp, long LastTimestamp) {
    public static LocalMenuCreditsState Start(TimeProvider clock) {
        long timestamp = clock.GetTimestamp();
        return new(timestamp, timestamp);
    }

    public (LocalMenuCreditsState State, MenuCredits? Frame) Read(TimeProvider clock) {
        long current = clock.GetTimestamp();
        TimeSpan elapsed;
        try {
            elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        } catch (OverflowException) {
            return (new(current, current), new(0));
        }
        // A faulty counter restarts the local visit instead of skipping directly to its return
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return (new(current, current), new(0));
        if (elapsed >= TimeSpan.FromMilliseconds(MenuCredits.DurationMilliseconds)) return (this, null);
        // Convert only after the strict duration bound prevents any integer overflow
        return (this with { LastTimestamp = current }, new((int)(elapsed.Ticks / TimeSpan.TicksPerMillisecond)));
    }
}
