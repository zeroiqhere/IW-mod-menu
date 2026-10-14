namespace syndishanx_iw_tool.Overlay.Themes;

public enum ThemeColorOperation { FixedFadeColor, DirectColor }

// This is a callback request, not an interpolation sample or native write permission
public sealed record ThemeColorCommand {
    public ThemeWidgetIdentity Widget { get; }
    public ThemeColorOperation Operation { get; }
    public ThemeRgb Target { get; }
    public int DurationMilliseconds { get; }

    internal ThemeColorCommand(ThemeWidgetIdentity widget, ThemeColorOperation operation,
        ThemeRgb target, int durationMilliseconds) {
        Widget = widget;
        Operation = operation;
        Target = target;
        // Exact source durations remain distinct from any future engine clock
        DurationMilliseconds = durationMilliseconds;
    }
}
