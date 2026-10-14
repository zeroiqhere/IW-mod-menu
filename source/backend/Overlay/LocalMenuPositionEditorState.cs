namespace syndishanx_iw_tool.Overlay;

// Offset belongs to the current editor visit and resets on the next entry
public sealed record LocalMenuPositionEditorState(int Step = 1) {
    public bool IsSupported() => Step is >= 1 and <= 10;
    public bool TryAdjustStep(int direction, out LocalMenuPositionEditorState updated) {
        updated = this;
        if (!IsSupported() || direction is not (-1 or 1)) return false;
        // Grenade adjustments stop at either bound rather than wrapping
        updated = this with { Step = System.Math.Clamp(Step + direction, 1, 10) };
        return true;
    }
}
