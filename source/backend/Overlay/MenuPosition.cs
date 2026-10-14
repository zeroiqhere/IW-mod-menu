namespace syndishanx_iw_tool.Overlay;

// Signed source coordinates are retained without a screen-edge clamp
public sealed record MenuPosition(int X, int Y) {
    public static MenuPosition Resolve(MenuAppearance appearance) {
        System.ArgumentNullException.ThrowIfNull(appearance);
        return appearance.MenuPosition ?? new(0, 85);
    }

    public bool TryMove(OverlayInput input, int step, out MenuPosition updated) {
        updated = this;
        if (step is < 1 or > 10) return false;
        long x = X, y = Y;
        switch (input) {
            case OverlayInput.MoveMenuUp: y -= step; break;
            case OverlayInput.MoveMenuDown: y += step; break;
            case OverlayInput.MoveMenuLeft: x -= step; break;
            case OverlayInput.MoveMenuRight: x += step; break;
            default: return false;
        }
        // Validate both coordinates before creating a committed appearance value
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue) return false;
        updated = new((int)x, (int)y);
        return true;
    }
}
