namespace syndishanx_iw_tool.Overlay;

// Source positions are signed coordinates, with no screen-edge clamp
public sealed record InstructionPosition(int X, int Y) {
    public static InstructionPosition Resolve(MenuAppearance appearance) => appearance.InstructionPosition
        ?? new InstructionPosition(appearance.AlternateInstructions ? 320 : 255, 472);

    public bool TryMove(OverlayInput input, out InstructionPosition updated) {
        updated = this;
        var offset = input switch {
            OverlayInput.MoveInstructionsUp => (X: 0, Y: -8),
            OverlayInput.MoveInstructionsDown => (X: 0, Y: 8),
            OverlayInput.MoveInstructionsLeft => (X: -8, Y: 0),
            OverlayInput.MoveInstructionsRight => (X: 8, Y: 0),
            _ => (X: 0, Y: 0)
        };
        if (offset == (0, 0)) return false;
        // Wider arithmetic rejects overflow before publishing a new signed coordinate
        long x = (long)X + offset.X;
        long y = (long)Y + offset.Y;
        if (x is < int.MinValue or > int.MaxValue || y is < int.MinValue or > int.MaxValue) return false;
        updated = new((int)x, (int)y);
        return true;
    }
}
