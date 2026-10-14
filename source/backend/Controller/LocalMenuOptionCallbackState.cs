using Apparition.Backend.Ipc;

namespace Apparition.Backend.Controller;

// The callback monitor clock is separate from the task that performs its action
internal sealed record LocalMenuOptionCallbackState(ulong Revision, string Kind, long StartedTimestamp,
    long LastTimestamp, bool PresentationApplied = false, int BaseMilliseconds = 0) {
    private const int BooleanRefreshMilliseconds = 180;
    private const int BooleanWaitMilliseconds = 430;
    private const int PlainWaitMilliseconds = 250;

    public static LocalMenuOptionCallbackState Start(ulong revision, string kind, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(clock);
        // Validate the supplied owner epoch before sampling any external clock
        if (revision == 0) throw new ArgumentOutOfRangeException(nameof(revision));
        if (kind is not ("boolean" or "plain")) {
            throw new ArgumentException("Callback kind must be boolean or plain", nameof(kind));
        }
        long timestamp = clock.GetTimestamp();
        return new(revision, kind, timestamp, timestamp);
    }

    public (LocalMenuOptionCallbackState? State, MenuOptionCallback? Frame, bool RefreshPresentation) Read(TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(clock);
        // A record copy cannot invent a completed redraw or an unrelated recovery offset
        if (!HasSupportedShape()) throw new InvalidOperationException("Unsupported callback clock state");
        long current = clock.GetTimestamp();
        TimeSpan elapsed;
        try {
            elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        } catch (OverflowException) {
            return Restart(current);
        }
        // Clock recovery preserves committed rows and starts only one finite remaining wait
        if (current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) {
            return Restart(current);
        }
        bool boolean = Kind == "boolean";
        bool refresh = boolean && !PresentationApplied
            && elapsed >= TimeSpan.FromMilliseconds(BooleanRefreshMilliseconds - BaseMilliseconds);
        int waitMilliseconds = boolean ? BooleanWaitMilliseconds : PlainWaitMilliseconds;
        // Late publication still requests its redraw before the controller retires this state
        if (elapsed >= TimeSpan.FromMilliseconds(waitMilliseconds - BaseMilliseconds)) {
            return (null, null, refresh);
        }
        string phase = boolean && !PresentationApplied
            && elapsed < TimeSpan.FromMilliseconds(BooleanRefreshMilliseconds) ? "pending" : "cooldown";
        return (this with { LastTimestamp = current }, new(Revision, Kind, phase), refresh);
    }

    private bool HasSupportedShape() => Revision != 0 && (Kind is "boolean" or "plain")
        && (Kind == "plain" ? !PresentationApplied && BaseMilliseconds == 0
            : BaseMilliseconds == 0 || PresentationApplied && BaseMilliseconds == BooleanRefreshMilliseconds);

    private (LocalMenuOptionCallbackState State, MenuOptionCallback Frame, bool RefreshPresentation) Restart(long current) {
        // The stage offset avoids subtracting timestamps or repeating an already applied redraw
        int stage = PresentationApplied ? BooleanRefreshMilliseconds : 0;
        var restarted = this with { StartedTimestamp = current, LastTimestamp = current, BaseMilliseconds = stage };
        string phase = Kind == "boolean" && !PresentationApplied ? "pending" : "cooldown";
        return (restarted, new(Revision, Kind, phase), false);
    }
}
