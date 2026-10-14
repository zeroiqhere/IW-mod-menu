namespace syndishanx_iw_tool.Overlay;

// A local preference rebuild has no committed-design footer override
public sealed record MenuPreferenceRebuild(string Phase) {
    public bool IsSupported() => Phase is "rebuild" or "cooldown";
}
