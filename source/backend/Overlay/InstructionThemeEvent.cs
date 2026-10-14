namespace syndishanx_iw_tool.Overlay;

// One accepted fixed callback captures its role before the current menu can change
public sealed record InstructionThemeEvent(ulong Revision, string Target, string Accent,
    ulong ElapsedMilliseconds, int? PredecessorGapMilliseconds, bool StoppedRainbow) {
    public bool IsSupported() {
        if (Revision == 0 || Target is not ("outline" or "background")
            || PredecessorGapMilliseconds is < 0 or > 500 || Accent is null
            || Accent.Length != 7 || Accent[0] != '#') return false;
        // RGB syntax remains exact ASCII while accepting either letter case
        foreach (char digit in Accent.AsSpan(1)) {
            if (digit is not (>= '0' and <= '9') and not (>= 'A' and <= 'F') and not (>= 'a' and <= 'f')) return false;
        }
        return true;
    }
}
