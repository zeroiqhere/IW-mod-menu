namespace syndishanx_iw_tool.Overlay;

// The footer has its own creation lifetime, independent of the open menu body
public sealed record InstructionHudState(bool Exists, ulong? CreationRevision = null,
    ulong? AdmissionRevision = null, string? CreationDesign = null,
    bool? CreationAlternate = null, bool? CreationBackground = null,
    string? CreationAccent = null, bool? CreationRainbow = null) {
    public static InstructionHudState Absent { get; } = new(false);
    public bool IsSupported() => Exists
        ? CreationRevision is > 0 && AdmissionRevision is > 0
            && CreationDesign is "Apparition" or "Classic" or "Native" or "AIO" or "Basic"
            && CreationAlternate is not null && CreationBackground is not null
            && ValidCreationColor()
        : CreationRevision is null && AdmissionRevision is null && CreationDesign is null
            && CreationAlternate is null && CreationBackground is null
            && CreationAccent is null && CreationRainbow is null;

    private bool ValidCreationColor() {
        // Both missing fields preserve the six-field legacy descriptor
        if (CreationAccent is null) return CreationRainbow is null;
        if (CreationRainbow is null || CreationAccent.Length != 7 || CreationAccent[0] != '#') return false;
        // ASCII RGB syntax accepts either case, without whitespace or numeric coercion
        foreach (char digit in CreationAccent.AsSpan(1)) {
            if (digit is not (>= '0' and <= '9') and not (>= 'A' and <= 'F') and not (>= 'a' and <= 'f')) return false;
        }
        return true;
    }
}
