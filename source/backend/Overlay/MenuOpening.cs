namespace syndishanx_iw_tool.Overlay;

// One opening event describes drawn row opacity without extending menu input locks
public sealed record MenuOpening(ulong Revision, int ElapsedMilliseconds) {
    public bool IsSupported(int rows, MenuAppearance appearance) => rows is >= 0 and <= 128 && Revision > 0 && ElapsedMilliseconds >= 0
        && ElapsedMilliseconds < MenuOptionCapacity.OpeningDurationMilliseconds(rows, appearance);
}
