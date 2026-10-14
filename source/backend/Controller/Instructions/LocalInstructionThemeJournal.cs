using syndishanx_iw_tool.Overlay;

namespace Apparition.Backend.Controller;

// The accepted invocation counter is independent of publication and footer creation counters
internal sealed record LocalInstructionThemeJournal(ulong Counter = 0, object? Owner = null,
    ulong? CreationRevision = null, bool PrefixComplete = true,
    IReadOnlyList<LocalInstructionThemeInvocation>? Invocations = null, long? LastTimestamp = null,
    LocalInstructionThemeInvocation? LastOutline = null, LocalInstructionThemeInvocation? LastBackground = null) {
    private IReadOnlyList<LocalInstructionThemeInvocation> Stored => Invocations ?? Array.Empty<LocalInstructionThemeInvocation>();

    public LocalInstructionThemeJournal Append(object owner, InstructionHudState hud, string target,
        string accent, bool stoppedRainbow, long timestamp, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(clock);
        // A disabled background removes both callback rectangles without creating an invocation
        if (!Owned(hud) || hud.CreationBackground != true) return this;
        if (!new InstructionThemeEvent(1, target, accent, 0, null, stoppedRainbow).IsSupported())
            throw new ArgumentException("Unsupported instruction theme invocation");
        // All counter and clock work precedes the caller's palette and mode mutations
        ulong revision = checked(Counter + 1);
        // A new actor or footer starts a new window while keeping the global counter
        var bound = Bind(owner, hud);
        if (bound.LastTimestamp is long last && timestamp < last) bound = bound.RetireTiming(timestamp);
        var predecessor = target == "outline" ? bound.LastOutline : bound.LastBackground;
        int? gap = null;
        if (predecessor is not null) {
            if (!Age(clock, predecessor.StartedTimestamp, timestamp, out ulong elapsed)) bound = bound.RetireTiming(timestamp);
            // Five hundred means the preceding request had a full quiet duration
            else gap = (int)Math.Min(500UL, elapsed);
        }
        var request = new LocalInstructionThemeInvocation(revision, target, accent, stoppedRainbow, timestamp, gap);
        var requests = bound.Stored.Append(request).ToArray();
        bool complete = bound.PrefixComplete && requests.Length <= 8;
        // Prefix loss remains explicit even after all retained requests reach their endpoints
        if (requests.Length > 8) requests = requests[^8..];
        // Only two channel ancestors survive pruning, keeping storage bounded for the entire owner lifetime
        return bound with { Counter = revision, Invocations = Array.AsReadOnly(requests), PrefixComplete = complete,
            LastTimestamp = timestamp, LastOutline = target == "outline" ? request : bound.LastOutline,
            LastBackground = target == "background" ? request : bound.LastBackground };
    }

    public (LocalInstructionThemeJournal State, InstructionThemeState? Frame) Project(object? owner,
        InstructionHudState? hud, TimeProvider clock) {
        ArgumentNullException.ThrowIfNull(clock);
        // Unavailable proof withdraws the window instead of borrowing an older actor
        if (owner is null || !Owned(hud)) return (new(Counter), null);
        var bound = Bind(owner, hud!);
        // An owned empty window is authoritative and requires no source clock read
        if (bound.Stored.Count == 0) return (bound, new(hud!.CreationRevision!.Value, bound.PrefixComplete, Array.Empty<InstructionThemeEvent>()));
        // One sample owns every age in this report, without rounding through floating-point JSON values
        long timestamp = clock.GetTimestamp();
        if (bound.LastTimestamp is long last && timestamp < last) return Recovery(bound, hud!, timestamp);
        var events = new List<InstructionThemeEvent>(bound.Stored.Count);
        foreach (var request in bound.Stored) {
            if (!Age(clock, request.StartedTimestamp, timestamp, out ulong elapsed)) return Recovery(bound, hud!, timestamp);
            events.Add(new(request.Revision, request.Target, request.Accent, elapsed, request.PredecessorGapMilliseconds, request.StoppedRainbow));
        }
        // The caller adopts this staged timing only after the complete state passes validation
        return (bound with { LastTimestamp = timestamp }, new(hud!.CreationRevision!.Value, bound.PrefixComplete, events));
    }

    private LocalInstructionThemeJournal Bind(object owner, InstructionHudState hud) =>
        ReferenceEquals(Owner, owner) && CreationRevision == hud.CreationRevision ? this
            : new(Counter, owner, hud.CreationRevision);

    // Timing recovery loses history but cannot reset the accepted invocation identity
    private LocalInstructionThemeJournal RetireTiming(long timestamp) => this with {
        PrefixComplete = false, Invocations = Array.Empty<LocalInstructionThemeInvocation>(),
        LastOutline = null, LastBackground = null, LastTimestamp = timestamp
    };

    private static (LocalInstructionThemeJournal State, InstructionThemeState Frame) Recovery(
        LocalInstructionThemeJournal state, InstructionHudState hud, long timestamp) {
        // Recovery cannot turn an unknown prefix into a fresh complete creation history
        var retired = state.RetireTiming(timestamp);
        return (retired, new(hud.CreationRevision!.Value, false, Array.Empty<InstructionThemeEvent>()));
    }

    private static bool Owned(InstructionHudState? hud) => hud is { Exists: true, CreationAccent: not null, CreationRainbow: not null }
        && hud.IsSupported();

    private static bool Age(TimeProvider clock, long started, long timestamp, out ulong milliseconds) {
        milliseconds = 0;
        // A rollback or unrepresentable elapsed span cannot create a restarted fade
        if (timestamp < started) return false;
        TimeSpan elapsed;
        try { elapsed = clock.GetElapsedTime(started, timestamp); }
        catch (OverflowException) { return false; }
        if (elapsed < TimeSpan.Zero || elapsed == TimeSpan.MaxValue) return false;
        milliseconds = (ulong)(elapsed.Ticks / TimeSpan.TicksPerMillisecond);
        return true;
    }
}
