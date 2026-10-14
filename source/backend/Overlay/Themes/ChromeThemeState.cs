namespace syndishanx_iw_tool.Overlay.Themes;

// Creation facts remain immutable while current membership records actual source object destruction
public sealed class ChromeThemeState {
    public bool Exists { get; }
    public ulong? CreationRevision { get; }
    public ulong? AdmissionRevision { get; }
    public string? CreationDesign { get; }
    public string? CreationAccent { get; }
    public bool? CreationRainbow { get; }
    public int CurrentRoleMask { get; }
    public bool PrefixComplete { get; }
    public IReadOnlyList<ChromeThemeEvent> Events { get; }

    public ChromeThemeState(bool exists, ulong? creationRevision, ulong? admissionRevision,
        string? creationDesign, string? creationAccent, bool? creationRainbow, int currentRoleMask,
        bool prefixComplete, IEnumerable<ChromeThemeEvent> events) {
        ArgumentNullException.ThrowIfNull(events);
        Exists = exists;
        CreationRevision = creationRevision;
        AdmissionRevision = admissionRevision;
        CreationDesign = creationDesign;
        CreationAccent = creationAccent;
        CreationRainbow = creationRainbow;
        CurrentRoleMask = currentRoleMask;
        PrefixComplete = prefixComplete;
        // Published event windows cannot borrow a mutable list from the caller
        Events = Array.AsReadOnly(events.ToArray());
    }

    public static ChromeThemeState Absent { get; } = new(false, null, null, null, null, null, 0, true, Array.Empty<ChromeThemeEvent>());

    public bool IsSupported() {
        if (!Exists) return CreationRevision is null && AdmissionRevision is null && CreationDesign is null
            && CreationAccent is null && CreationRainbow is null && CurrentRoleMask == 0 && PrefixComplete && Events.Count == 0;
        int created = ChromeThemeRoles.CreationMask(CreationDesign);
        if (CreationRevision is null or 0 || AdmissionRevision is null or 0 || created == 0
            || !ChromeThemeRoles.Hex(CreationAccent) || CreationRainbow is null || Events.Count > 8
            || CurrentRoleMask != created && CurrentRoleMask != (created & ~ChromeThemeRoles.Title)) return false;
        ChromeThemeEvent? previous = null;
        foreach (var request in Events) {
            if (request is null || !request.IsSupported(CreationDesign)) return false;
            if (previous is not null) {
                // An incomplete prefix never permits holes inside the retained tail
                if (request.Revision <= previous.Revision || request.Revision - previous.Revision != 1
                    || request.ElapsedMilliseconds > previous.ElapsedMilliseconds
                    || request.PredecessorGapMilliseconds is not int gap) return false;
                ulong difference = previous.ElapsedMilliseconds - request.ElapsedMilliseconds;
                // Independent floor-to-millisecond conversion can differ by one millisecond
                if (gap == 500 ? difference < 499
                    : difference > (ulong)gap ? difference - (ulong)gap > 1 : (ulong)gap - difference > 1) return false;
            } else if (PrefixComplete && request.PredecessorGapMilliseconds is not null) return false;
            previous = request;
        }
        return true;
    }
}
