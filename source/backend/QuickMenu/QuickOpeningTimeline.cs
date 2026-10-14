namespace Apparition.Backend.QuickMenu;

// No timer, task or menu operation is started by this presentation state
public sealed class QuickOpeningTimeline {
    public const int InputWaitMilliseconds = 550;
    private readonly TimeProvider clock;
    private readonly int shownRows;
    private readonly bool fadeCancelled;
    public long StartedTimestamp { get; }
    public long LastTimestamp { get; }
    public bool IsActive { get; }

    private QuickOpeningTimeline(TimeProvider clock, int shownRows, long started, long last, bool active, bool fadeCancelled) {
        this.clock = clock;
        this.shownRows = shownRows;
        this.fadeCancelled = fadeCancelled;
        StartedTimestamp = started;
        LastTimestamp = last;
        IsActive = active;
    }

    public static QuickOpeningTimeline Start(TimeProvider clock, int shownRows) {
        ArgumentNullException.ThrowIfNull(clock);
        if (shownRows is < 0 or > 14) throw new ArgumentOutOfRangeException(nameof(shownRows));
        // Source Quick opening draws at admission instead of waiting for Main's 50 ms rebuild
        long timestamp = clock.GetTimestamp();
        return new(clock, shownRows, timestamp, timestamp, true, false);
    }

    public QuickOpeningObservation Read() {
        if (!IsActive) return new(this, new(false, false, null, 0));
        long current = clock.GetTimestamp();
        TimeSpan elapsed;
        bool overflow = false;
        try {
            elapsed = clock.GetElapsedTime(StartedTimestamp, current);
        } catch (OverflowException) {
            // Runtime versions may throw rather than saturate an elapsed value outside TimeSpan
            elapsed = TimeSpan.Zero;
            overflow = true;
        }
        bool recovered = overflow || current < LastTimestamp || elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue;
        // Clock recovery is a native adaptation: keep the visible HUD and restart only a finite input wait
        long started = recovered ? current : StartedTimestamp;
        bool cancelled = fadeCancelled || recovered;
        if (recovered) elapsed = TimeSpan.Zero;
        var next = new QuickOpeningTimeline(clock, shownRows, started, current, true, cancelled);
        int duration = Math.Max(0, shownRows - 1) * 100;
        bool locked = elapsed < TimeSpan.FromMilliseconds(InputWaitMilliseconds);
        int? opening = !cancelled && duration > 0 && elapsed < TimeSpan.FromMilliseconds(duration)
            ? (int)elapsed.TotalMilliseconds : null;
        // A long row fade cannot hold the source monitor after its separate 550 ms wait
        return new(next, new(true, locked, opening, duration));
    }

    // Actual row publication binds opacity without restarting the source input deadline
    public QuickOpeningTimeline WithShownRows(int rows) {
        if (rows is < 0 or > 14) throw new ArgumentOutOfRangeException(nameof(rows));
        return rows == shownRows ? this : new(clock, rows, StartedTimestamp, LastTimestamp, IsActive, fadeCancelled);
    }

    // Fade cancellation keeps the independent ordinary-monitor wait
    public QuickOpeningTimeline CancelFade() => !IsActive || fadeCancelled ? this
        : new(clock, shownRows, StartedTimestamp, LastTimestamp, true, true);

    public QuickOpeningTimeline Cancel() {
        if (!IsActive) return this;
        // Cancellation retains no pending callback that could reopen the menu later
        return new(clock, shownRows, StartedTimestamp, LastTimestamp, false, true);
    }
}
