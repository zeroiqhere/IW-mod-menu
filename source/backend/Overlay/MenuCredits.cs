namespace syndishanx_iw_tool.Overlay;

// Credits use a bounded elapsed value rather than a clock shared with the renderer
public sealed record MenuCredits(int ElapsedMilliseconds) {
    public const int DurationMilliseconds = 40000;
    public bool IsSupported() => ElapsedMilliseconds is >= 0 and < DurationMilliseconds;
}
