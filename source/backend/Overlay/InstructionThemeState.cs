namespace syndishanx_iw_tool.Overlay;

// The bounded window owns its event copies and never invents a missing invocation prefix
public sealed class InstructionThemeState {
    public ulong CreationRevision { get; }
    public bool PrefixComplete { get; }
    public IReadOnlyList<InstructionThemeEvent> Events { get; }

    public InstructionThemeState(ulong creationRevision, bool prefixComplete, IEnumerable<InstructionThemeEvent> events) {
        ArgumentNullException.ThrowIfNull(events);
        CreationRevision = creationRevision;
        PrefixComplete = prefixComplete;
// Copying the caller list prevents later edits from changing an already published window
        Events = Array.AsReadOnly(events.ToArray());
    }

    public bool IsSupported() {
        if (CreationRevision == 0 || Events.Count > 8) return false;
        InstructionThemeEvent? previous = null;
        InstructionThemeEvent? outline = null;
        InstructionThemeEvent? background = null;
        foreach (var request in Events) {
            if (request is null || !request.IsSupported() || previous is not null
                && (request.Revision <= previous.Revision || request.ElapsedMilliseconds > previous.ElapsedMilliseconds)) return false;
            // Prefix loss permits missing earlier requests, never holes inside the retained tail
            if (previous is not null && request.Revision - previous.Revision != 1) return false;
            // Each rectangle has its own predecessor even when the other channel intervenes
            var predecessor = request.Target == "outline" ? outline : background;
            if (predecessor is not null) {
                if (request.PredecessorGapMilliseconds is not int gap) return false;
                ulong difference = predecessor.ElapsedMilliseconds - request.ElapsedMilliseconds;
                // Separately floored millisecond ages can differ from the invocation gap by one
                if (gap == 500 ? difference < 499
                    : difference > (ulong)gap ? difference - (ulong)gap > 1 : (ulong)gap - difference > 1) return false;
            // An incomplete prefix may begin with a predecessor outside the retained eight requests
            } else if (PrefixComplete && request.PredecessorGapMilliseconds is not null) return false;
            if (request.Target == "outline") outline = request;
            else background = request;
            previous = request;
        }
        return true;
    }
}
