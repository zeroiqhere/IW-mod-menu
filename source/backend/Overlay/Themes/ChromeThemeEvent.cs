namespace syndishanx_iw_tool.Overlay.Themes;

// One fixed invocation addresses the captured existing-role mask with a shared source duration
public sealed record ChromeThemeEvent(ulong Revision, int TargetMask, string Accent,
    ulong ElapsedMilliseconds, int? PredecessorGapMilliseconds, bool StoppedRainbow) {
    public bool IsSupported(string? creationDesign) => Revision > 0
        && TargetMask != 0 && TargetMask == ChromeThemeRoles.FixedMask(creationDesign)
        && ChromeThemeRoles.Hex(Accent) && PredecessorGapMilliseconds is not (< 0 or > 500);
}
