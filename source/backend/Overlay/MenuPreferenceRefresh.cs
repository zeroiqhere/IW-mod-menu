namespace syndishanx_iw_tool.Overlay;

// This visible menu pause is distinct from a soft unlock that rebuilds a hidden panel
public sealed record MenuPreferenceRefresh(string Phase) {
    public bool IsSupported() => Phase is "pending" or "cooldown" or "selector";
}
