namespace syndishanx_iw_tool.Overlay;

// Normal opening owns a distinct synchronous monitor wait without changing gameplay state
public sealed record MenuOpenTransition(string Phase) {
    public bool IsSupported() => Phase is "rebuild" or "cooldown";
}
