using syndishanx_iw_tool.Overlay.Themes;

namespace Apparition.Backend.Controller;

// The bounded creation journal survives row redraws without owning native presented colors
internal sealed record LocalChromeThemeJournal(ulong Counter = 0, object? Owner = null,
    ChromeThemeState? Created = null, IReadOnlyList<LocalChromeThemeInvocation>? Invocations = null,
    bool PrefixComplete = true, long? LastTimestamp = null, LocalChromeThemeInvocation? LastInvocation = null) {
    private IReadOnlyList<LocalChromeThemeInvocation> Stored => Invocations ?? Array.Empty<LocalChromeThemeInvocation>();

    public LocalChromeThemeJournal Append(object owner, ChromeThemeState frame, int mask,
        string accent, bool stoppedRainbow, long timestamp, TimeProvider clock) {
        // Fixed selections cannot address title-free Width chrome or a changed creation design
        if (!ReferenceEquals(Owner, owner) || Created?.CreationRevision != frame.CreationRevision
            || (frame.CurrentRoleMask & mask) != mask || !frame.Exists) return this;
        var validated = new ChromeThemeEvent(1, mask, accent, 0, null, stoppedRainbow);
        if (!validated.IsSupported(frame.CreationDesign)) throw new ArgumentException("Unsupported chrome invocation");
        ulong revision = checked(Counter + 1);
        var bound = this;
        if (LastTimestamp is long last && timestamp < last) bound = Recover(timestamp);
        int? gap = null;
        if (bound.LastInvocation is { } predecessor) {
            if (!Age(clock, predecessor.StartedTimestamp, timestamp, out ulong elapsed)) bound = bound.Recover(timestamp);
            else gap = (int)Math.Min(500UL, elapsed);
        }
        var request = new LocalChromeThemeInvocation(revision, mask, accent, stoppedRainbow, timestamp, gap);
        var tail = bound.Stored.Append(request).ToArray();
        bool complete = bound.PrefixComplete && tail.Length <= 8;
        if (tail.Length > 8) tail = tail[^8..];
        // Prefix loss remains explicit for this creation even after a later quiet endpoint
        return bound with { Counter = revision, Invocations = Array.AsReadOnly(tail), PrefixComplete = complete,
            LastTimestamp = timestamp, LastInvocation = request };
    }

    public (LocalChromeThemeJournal State, ChromeThemeState Frame) Project(object owner, ulong creation,
        ulong admission, string design, string accent, bool rainbow, bool titleDestroyed, TimeProvider clock) {
        var bound = this;
        if (!ReferenceEquals(Owner, owner) || Created?.CreationRevision != creation) {
            // The actual successful redraw supplies creation configuration, never a later palette value
            var header = new ChromeThemeState(true, creation, admission, design, accent, rainbow,
                ChromeThemeRoles.CreationMask(design), true, Array.Empty<ChromeThemeEvent>());
            bound = new(Counter, owner, header);
        }
        int members = bound.Created!.CurrentRoleMask;
        if (titleDestroyed) members &= ~ChromeThemeRoles.Title;
        var events = new List<ChromeThemeEvent>();
        if (bound.Stored.Count > 0) {
            // One report timestamp owns every unclamped age
            long timestamp = clock.GetTimestamp();
            if (bound.LastTimestamp is long last && timestamp < last) bound = bound.Recover(timestamp);
            else {
                foreach (var request in bound.Stored) {
                    if (!Age(clock, request.StartedTimestamp, timestamp, out ulong elapsed)) {
                        bound = bound.Recover(timestamp);
                        events.Clear();
                        break;
                    }
                    events.Add(new(request.Revision, request.TargetMask, request.Accent, elapsed,
                        request.PredecessorGapMilliseconds, request.StoppedRainbow));
                }
                bound = bound with { LastTimestamp = timestamp };
            }
        }
        var captured = bound.Created!;
        var frame = new ChromeThemeState(true, creation, admission, captured.CreationDesign,
            captured.CreationAccent, captured.CreationRainbow, members, bound.PrefixComplete, events);
        // Store the destroyed membership too, so it cannot resurrect on a later same-ID page
        return (bound with { Created = frame }, frame);
    }

    public LocalChromeThemeJournal Retire() => new(Counter);
    private LocalChromeThemeJournal Recover(long timestamp) => this with {
        PrefixComplete = false, Invocations = Array.Empty<LocalChromeThemeInvocation>(),
        LastInvocation = null, LastTimestamp = timestamp
    };
    private static bool Age(TimeProvider clock, long started, long timestamp, out ulong milliseconds) {
        milliseconds = 0;
        if (timestamp < started) return false;
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(started, timestamp); }
        catch (OverflowException) { return false; }
        if (elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return false;
        milliseconds = (ulong)(elapsed.Ticks / TimeSpan.TicksPerMillisecond);
        return true;
    }
}
