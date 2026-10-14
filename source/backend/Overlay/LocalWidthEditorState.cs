namespace syndishanx_iw_tool.Overlay;

// Offset belongs to one editor visit while committed width belongs to appearance
public sealed record LocalWidthEditorState(int Step = 1) {
    public const int MinimumWidth = 200;
    public const int MaximumWidth = 500;
    public bool IsSupported() => Step is >= 1 and <= 10;
    public bool TryAdjustWidth(int width, int direction, out int updated) {
        updated = width;
        if (!IsSupported() || width is < MinimumWidth or > MaximumWidth || direction is not (-1 or 1)) return false;
        // Each adjustment commits immediately and the last step may be smaller at a boundary
        updated = System.Math.Clamp(width + direction * Step, MinimumWidth, MaximumWidth);
        return true;
    }
    public bool TryAdjustStep(int direction, out LocalWidthEditorState updated) {
        updated = this;
        if (!IsSupported() || direction is not (-1 or 1)) return false;
        // Source offset changes stop at either end rather than wrapping
        updated = this with { Step = System.Math.Clamp(Step + direction, 1, 10) };
        return true;
    }
}
